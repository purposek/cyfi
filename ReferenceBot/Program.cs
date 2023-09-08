using Domain.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReferenceBot.Services;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using Domain.Enums;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReferenceBot.Services.Interfaces;

namespace ReferenceBot
{
    public class Program
    {

        private static IConfigurationRoot Configuration;

        private static Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> GameStateDict = new();
        private static void Main(string[] args)
        {

            // Set up configuration sources.
            using var host = CreateHostBuilder(args).Build();

            using var serviceScope = host.Services.CreateScope();
            var provider = serviceScope.ServiceProvider;

            var pathfindingService = provider.GetRequiredService<IPathfindingService>();
            var pathTraversalService = provider.GetRequiredService<IPathTraversalService>();
            var adversarialDecisionService = provider.GetRequiredService<IAdversarialDecisionService>();

            // Set up configuration sources.
            var builder = new ConfigurationBuilder().AddJsonFile(
                $"appsettings.json",
                optional: false
            );

            Configuration = builder.Build();
            var environmentIp = Environment.GetEnvironmentVariable("RUNNER_IPV4");
            var ip = !string.IsNullOrWhiteSpace(environmentIp)
                ? environmentIp
                : Configuration.GetSection("RunnerIP").Value;
            ip = ip.StartsWith("http://") ? ip : "http://" + ip;

            var botNickname =
                Environment.GetEnvironmentVariable("BOT_NICKNAME")
                ?? Configuration.GetSection("BotNickname").Value;

            var token =
                Environment.GetEnvironmentVariable("Token") ??
                Environment.GetEnvironmentVariable("REGISTRATION_TOKEN");

            var port = Configuration.GetSection("RunnerPort");

            var url = ip + ":" + port.Value + "/runnerhub";

            var connection = new HubConnectionBuilder()
                .WithUrl($"{url}")
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .WithAutomaticReconnect()
                .Build();

            connection.StartAsync();

            Console.WriteLine("Connected to Runner");

            connection.On<Guid>("Registered", (id) =>
            {
                pathTraversalService.SetBotId(id);
            });

            connection.On<String>(
                "Disconnect",
                async (reason) =>
                {
                    Console.WriteLine($"Server sent disconnect with reason: {reason}");
                    await connection.StopAsync();
                }
            );

            connection.On<BotStateDTO>(
                "ReceiveBotState",
                (botState) =>
                {
                    var timestamp = DateTime.Now;
                    GameStateDict[botState.GameTick] = (botState.CurrentPosition, botState.CurrentState, InputCommand.None, !GameStateDict.ContainsKey(botState.GameTick - 1) || GameStateDict[botState.GameTick - 1].Level != botState.CurrentLevel ? new Point(0, 0) : new Point(botState.X - GameStateDict[botState.GameTick - 1].Position.X, botState.Y - GameStateDict[botState.GameTick - 1].Position.Y), botState.CurrentLevel);
                    PositionHistory.AddPosition(botState.CurrentPosition);
                    BotCommand command;
                    command = pathTraversalService.NextCommand(botState, GameStateDict);
                    if (WorldMapPerspective.OpponentsInCloseRange) command = adversarialDecisionService.NextCommand(command, botState, GameStateDict);
                    connection.InvokeAsync("SendPlayerCommand", command);
                    var timestamp2 = DateTime.Now;
                    
                    CommandHistory.AddCommand(command.Action);
                    WorldMapPerspective.UpdateState(botState);
                    Console.WriteLine($"Received at {timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}");
                    Console.WriteLine($"{botNickname} -> X: {botState.X}, Y: {botState.Y}, Level {botState.CurrentLevel}, Tick {botState.GameTick}, Col: {botState.Collected}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Sent {command.Action} after {timestamp2.Subtract(timestamp).Ticks / 10000d:F4} ms");
                    Console.ResetColor();
                    GameStateDict[botState.GameTick] = (botState.CurrentPosition, botState.CurrentState, command.Action, !GameStateDict.ContainsKey(botState.GameTick - 1) || GameStateDict[botState.GameTick - 1].Level != botState.CurrentLevel ? new Point(0, 0) : new Point(botState.X - GameStateDict[botState.GameTick - 1].Position.X, botState.Y - GameStateDict[botState.GameTick - 1].Position.Y), botState.CurrentLevel);
                }
            );

            connection.Closed += (error) =>
            {
                Console.WriteLine($"Server closed with error: {error}");
                return Task.CompletedTask;
            };

            connection.InvokeAsync("Register", token, botNickname);

            Console.WriteLine(connection.State);
            while (connection.State == HubConnectionState.Connected || connection.State == HubConnectionState.Connecting)
            {
                Thread.Sleep(300);
                //Console.WriteLine(connection.State);
            }
        }


        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureServices(GetServiceConfiguration);

        private static void GetServiceConfiguration(HostBuilderContext _, IServiceCollection services)
        {
            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", optional: false);
            Configuration = builder.Build();

            services.AddSingleton<IPathfindingService, PathfindingService>();
            services.AddSingleton<IPathTraversalService, PathTraversalService>();
            services.AddSingleton<IAdversarialDecisionService, AdversarialDecisionService>();
        }
    }
}