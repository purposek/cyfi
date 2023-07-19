using Domain.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReferenceBot.Services;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using Domain.Enums;
using System.Collections.Generic;

namespace ReferenceBot
{
    public class Program
    {

        private static IConfigurationRoot Configuration;
        private static Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> GameStateDict = new Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)>();

        private static void Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("DOCKER") != null)
            {
                Console.WriteLine("Docker detected, disabling GUI...");
            }

            BotService botService = new();

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
                botService.SetBotId(id);
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
                    Console.WriteLine($"{botNickname} -> X: {botState.X}, Y: {botState.Y}, Level {botState.CurrentLevel}, Tick {botState.GameTick}, Col: {botState.Collected}");
                    GameStateDict[botState.GameTick] = (botState.CurrentPosition, botState.CurrentState, InputCommand.None, !GameStateDict.ContainsKey(botState.GameTick - 1) || GameStateDict[botState.GameTick - 1].Level != botState.CurrentLevel ? new Point(0, 0) : new Point(botState.X - GameStateDict[botState.GameTick - 1].Position.X, botState.Y - GameStateDict[botState.GameTick - 1].Position.Y), botState.CurrentLevel);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    Console.WriteLine($"Recived at {timestamp}");
                    Stopwatch sw = Stopwatch.StartNew();
                    BotCommand command = botService.ProcessState(botState, GameStateDict);
                    sw.Stop();
                    Console.WriteLine($"Evaluating command took {sw.ElapsedMilliseconds}ms");
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    Console.WriteLine($"Sent at {timestamp}");
                    connection.InvokeAsync("SendPlayerCommand", command);
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
    }
}
