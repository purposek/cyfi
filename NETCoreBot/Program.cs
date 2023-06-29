using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using NETCoreBot.Models;
using NETCoreBot.Services;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NETCoreBot.Enums;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Drawing;

namespace NETCoreBot
{
    public class Program
    {
        public static IConfigurationRoot Configuration;
        private static Guid Id = Guid.NewGuid();
        private static int level = 0;
        private static HubConnection connection;
        private static Tuple<int, int> targetCollectablePoint = null;
        private static double distTargetCollectablePoint = Double.MaxValue;
        private static Tuple<int, int> expectedResultantPosition = null;
        private static List<Tuple<int, int>> collectiblePoints = new List<Tuple<int, int>>();
        private static Queue<InputCommand> nextCommands = new Queue<InputCommand>();
        private static bool invalidatingWorld;
        private static bool startMotion;
        private static bool dashing;
        private static List<List<InputCommand>> singleMovePermutations;
        private static List<List<InputCommand>> doubleMovePermutations;
        private static List<List<InputCommand>> tripleMovePermutations;
        private static List<List<InputCommand>> fallingPermutations;
        private static InputCommand lastCommandSent = InputCommand.None;
        private static BotStateDTO botStatus;
        private static async Task Main(string[] args)
        {
            List<InputCommand> commands = new List<InputCommand>()
        {
            InputCommand.DIGDOWN,
            InputCommand.DOWNLEFT,
            InputCommand.DOWNRIGHT,
            InputCommand.DIGLEFT,
            InputCommand.DIGRIGHT,
            InputCommand.UPLEFT,
            InputCommand.UPRIGHT,
            InputCommand.UP,
            InputCommand.LEFT,
            InputCommand.RIGHT,
        };



            singleMovePermutations = GeneratePermutations(commands, 1);
            doubleMovePermutations = GeneratePermutations(commands, 2);
            tripleMovePermutations = GeneratePermutations(commands, 3);


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

            connection = new HubConnectionBuilder()
                .WithUrl($"{url}")
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .WithAutomaticReconnect()
                .Build();

            var botService = new BotService();

            await connection.StartAsync();
            Console.WriteLine("Connected to Runner");

            connection.On<Guid>("Registered", (id) => {
                Id = id;
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
                (botStateDTO) =>
                {
                    botService.SetBotState(botStateDTO);
                    botStatus = botStateDTO;
                    UpdatePerspectives(botStateDTO);
                    collectiblePoints = collectiblePoints.Except(collectiblePoints.Where(x => x.Item2 > botStateDTO.CurrentPosition.Item2 && WorldMapPerspective.ObjectCoordinates[x.Item1 - 1, x.Item2] == ObjectType.Solid && WorldMapPerspective.ObjectCoordinates[x.Item1 + 1, x.Item2] == ObjectType.Solid)).ToList();

                    //TODO Check if previous command evaluated

                    var currentPosition = new Tuple<int, int>(botStateDTO.X, botStateDTO.Y);
                    if (!nextCommands.Any() && botStateDTO.CurrentState != "Jumping" && botStateDTO.CurrentState != "Falling" && BotContainsLadder(currentPosition) && BotOnLadder(currentPosition) || BotOnSolid(botStateDTO.CurrentPosition) || BotContainsLadder(botStateDTO.CurrentPosition))
                    {
                        Tuple<int, int> target = ClosestLowerCollectablesWithinRange(currentPosition);

                        if (false && target != null)
                        {
                            if (target.Item1 == currentPosition.Item1)
                            {
                                nextCommands.Enqueue(InputCommand.DIGDOWN);
                            }
                            else
                            {
                                if (currentPosition.Item2 > target.Item2 + 1)
                                {
                                    nextCommands.Enqueue(target.Item1 > currentPosition.Item1 ? InputCommand.DOWNRIGHT : InputCommand.DOWNLEFT);
                                }
                                else
                                {
                                    nextCommands.Enqueue(target.Item1 > currentPosition.Item1 ? InputCommand.DIGRIGHT : InputCommand.DIGLEFT);
                                }
                            }
                        }
                    }

                    dashing = false;
                    if (!nextCommands.Any() && botStateDTO.CurrentState != "Jumping" && botStateDTO.CurrentState != "Falling" && (HeroOnPlatform(botStateDTO) || HeroOnLadder(botStateDTO) || BotOnSolid(botStateDTO.CurrentPosition) || BotContainsLadder(botStateDTO.CurrentPosition) || botStateDTO.CurrentPosition.Item2 == 0))
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        if (collectiblePoints.Any() && (targetCollectablePoint == null || !collectiblePoints.Contains(targetCollectablePoint) || distTargetCollectablePoint < DistanceFromHero(targetCollectablePoint, botStateDTO)))
                        {
                            UpdateNearestCollectablePoint(botStateDTO);
                        }

                        TryToDashOnPlatform(botStateDTO, currentPosition);
                        TryLeapOfFaithOnPlatform(botStateDTO, currentPosition);
                        //TryToDashOnPlatform(botStateDTO, currentPosition);

                        if (!dashing && targetCollectablePoint != null)
                        {
                            var collectionCommands = NextSafeCommands(botStateDTO, singleMovePermutations, EvaluationMode.Collect);
                            nextCommands = new Queue<InputCommand>(collectionCommands);
                            if (!nextCommands.Any())
                            {
                                //new List<List<InputCommand>> { new List<InputCommand>{InputCommand.DIGRIGHT, InputCommand.UP, InputCommand.DIGRIGHT} }
                                var approachingCommands = NextSafeCommands(botStateDTO, tripleMovePermutations, EvaluationMode.Approach);
                                nextCommands = new Queue<InputCommand>(approachingCommands);
                            }
                        }

                        stopwatch.Stop();
                        var t = stopwatch.ElapsedMilliseconds;
                    }

                    //if ((botStateDTO.CurrentState == "Idle" || botStateDTO.CurrentState == "Falling") && HeroOnAirOrCollectable(botStateDTO))
                    //{
                    //    nextCommands = chosenFallingCommands;
                    //}

                    if (false && botStateDTO.CurrentState == "Falling")
                    {
                        InputCommand collectingCommand = TryCollectAndLandSafely(new Tuple<int, int>(botStateDTO.X, botStateDTO.Y), EvaluationMode.Collect);
                        if (collectingCommand != InputCommand.None)
                        {
                            nextCommands.Clear();
                            nextCommands.Enqueue(collectingCommand);
                        }
                        else
                        {
                            //TODO Consider previous trajectory
                            InputCommand landingCommand = TryCollectAndLandSafely(new Tuple<int, int>(botStateDTO.X, botStateDTO.Y), EvaluationMode.Approach);
                            if (landingCommand != InputCommand.None)
                            {
                                nextCommands.Clear();
                                nextCommands.Enqueue(landingCommand);
                            }
                        }
                    }


                    if (!dashing && nextCommands.Any())
                    {
                        var nextCommand = nextCommands.Dequeue();
                        var botCommand = new BotCommand
                        {
                            Action = nextCommand,
                            BotId = Id
                        };
                        lastCommandSent = nextCommand;
                        if (targetCollectablePoint != null) distTargetCollectablePoint = DistanceFromHero(targetCollectablePoint, botStateDTO);

                        expectedResultantPosition = EvaluateResultantPosition(nextCommand, botStateDTO);

                        connection.InvokeAsync("SendPlayerCommand", botCommand);
                        //platformAndLadderCommands.Clear();
                    }
                });


            connection.Closed += (error) =>
            {
                Console.WriteLine($"Server closed with error: {error}");
                return Task.CompletedTask;
            };

            await connection.InvokeAsync("Register", token, botNickname);
            while (connection.State == HubConnectionState.Connected)
            {
                var state = botService.GetBotState();
                var botId = botService.GetBotId();
                if (state == null || botId == null)
                {
                    continue;
                }
                Console.WriteLine($"Bot ID: {botId}");
                Console.WriteLine(
                    $"Position: ({state.X}, {state.Y}), Collected: {state.Collected}, Level: {state.CurrentLevel}"
                );
                Console.WriteLine(state.PrintWindow());
            }
        }
        private static void TryLeapOfFaithOnPlatform(BotStateDTO botStateDTO, Tuple<int, int> currentPosition)
        {
            Tuple<int, int> platformTarget = ClosestPlatformCollectablesWithinRange(currentPosition);
            if (platformTarget != null)
            {
                InputCommand cmd;
                var dx = 0;
                if (platformTarget.Item1 > currentPosition.Item1)
                {
                    cmd = InputCommand.UPRIGHT;
                    dx = 1;
                }
                else
                {
                    cmd = InputCommand.UPLEFT;
                    dx = -1;
                }
                var nextPosition = new Tuple<int, int>(currentPosition.Item1 + dx, currentPosition.Item2);

                if (BotOnHazard(nextPosition) && !BotContainsLadder(currentPosition))
                {
                    var botCommand = new BotCommand
                    {
                        Action = cmd,
                        BotId = Id
                    };
                    lastCommandSent = cmd;
                    nextCommands.Clear();
                    expectedResultantPosition = EvaluateResultantPosition(cmd, botStateDTO);

                    connection.InvokeAsync("SendPlayerCommand", botCommand);
                }
                else if (BotOnHazard(nextPosition) && BotContainsLadder(currentPosition))
                {
                    var botCommand = new BotCommand
                    {
                        Action = InputCommand.UP,
                        BotId = Id
                    };
                    lastCommandSent = InputCommand.UP;
                    nextCommands.Clear();
                    expectedResultantPosition = EvaluateResultantPosition(InputCommand.UP, botStateDTO);

                    connection.InvokeAsync("SendPlayerCommand", botCommand);
                    connection.InvokeAsync("SendPlayerCommand", botCommand);
                }
            }
        }

        private static void TryToDashOnPlatform(BotStateDTO botStateDTO, Tuple<int, int> currentPosition)
        {
            if (AboutToLevelUp(botStateDTO))
            {
                return;
            }
            Tuple<int, int> platformTarget = ClosestPlatformCollectablesWithinRange(currentPosition);
            var resultantPosition = new Tuple<int, int>(currentPosition.Item1, currentPosition.Item2);
            var count = 0;
            while (count < 3 && platformTarget != null && platformTarget.Item1 != resultantPosition.Item1)
            {
                InputCommand cmd;
                var dx = 0;
                if (platformTarget.Item1 > currentPosition.Item1)
                {
                    cmd = InputCommand.DIGRIGHT;
                    dx = 1;
                }
                else
                {
                    cmd = InputCommand.DIGLEFT;
                    dx = -1;
                }
                resultantPosition = new Tuple<int, int>(resultantPosition.Item1 + dx, resultantPosition.Item2);

                if (BotOnPlatform(resultantPosition) && !BotOnHazard(resultantPosition) && !BotOnLadder(resultantPosition) && !BotContainsLadder(resultantPosition))
                {
                    var botCommand = new BotCommand
                    {
                        Action = cmd,
                        BotId = Id
                    };
                    lastCommandSent = cmd;
                    dashing = true;
                    nextCommands.Clear();
                    expectedResultantPosition = EvaluateResultantPosition(cmd, botStateDTO);

                    count++;
                    connection.InvokeAsync("SendPlayerCommand", botCommand);

                }
                else
                {
                    break;
                }
            }
        }

        private static bool AboutToLevelUp(BotStateDTO botStateDTO)
        {
            var c = botStateDTO.Collected;
            return (c > 17 && c < 21) || (c > 57 && c < 61) || (c > 87 && c < 91);
        }

        private static Tuple<int, int> ClosestLowerCollectablesWithinRange(Tuple<int, int> currentPosition)
        {
            return collectiblePoints.Where(x => Math.Abs(x.Item1 - currentPosition.Item1) < 11 && currentPosition.Item2 - x.Item2 >= 0).OrderBy(x => DistanceBetweenPoints(x, currentPosition)).FirstOrDefault();
        }

        private static Tuple<int, int> ClosestPlatformCollectablesWithinRange(Tuple<int, int> currentPosition)
        {
            return collectiblePoints.Where(x => Math.Abs(x.Item1 - currentPosition.Item1) < 6 && currentPosition.Item2 + 1 == x.Item2).OrderBy(x => DistanceBetweenPoints(x, currentPosition)).FirstOrDefault();
        }

        private static double DistanceBetweenPoints(Tuple<int, int> point, Tuple<int, int> targetPoint)
        {
            return Math.Sqrt(Math.Pow(targetPoint.Item1 - point.Item1, 2) + Math.Pow(targetPoint.Item2 - point.Item2, 2));
        }

        private static InputCommand TryCollectAndLandSafely(Tuple<int, int> resultantPosition, EvaluationMode collectMode)
        {
            var fallingCommands = new List<InputCommand> { InputCommand.LEFT, InputCommand.RIGHT };
            Dictionary<InputCommand, Tuple<double, double>> collectedDistFromTargetAndBot = new Dictionary<InputCommand, Tuple<double, double>>();
            foreach (var fallingCommand in fallingCommands)
            {
                EvaluateCollectsSafelyWhileFalling(resultantPosition, fallingCommand, collectMode, collectedDistFromTargetAndBot);
            }

            if (collectedDistFromTargetAndBot.Any())
            {
                return collectedDistFromTargetAndBot.OrderBy(x => x.Value.Item1).ThenByDescending(x => x.Value.Item2).First().Key;
            }
            return InputCommand.None;
        }

        private static void EvaluateCollectsSafelyWhileFalling(Tuple<int, int> resultantPosition, InputCommand fallingCommand, EvaluationMode collectMode, Dictionary<InputCommand, Tuple<double, double>> collectedDistFromTargetAndBot)
        {
            var deltaX = fallingCommand == InputCommand.LEFT ? -1 : 1;
            var tick = 0;
            var collected = collectMode != EvaluationMode.Collect;
            while (!ShouldAbortEvaluation(tick, resultantPosition))
            {
                tick++;
                resultantPosition = new Tuple<int, int>(resultantPosition.Item1 + deltaX, resultantPosition.Item2 - 1);
                if (CanCollect(resultantPosition))
                {
                    collected = true;
                }

                if ((BotOnLadder(resultantPosition) || BotOnPlatform(resultantPosition)) && collected && !ShouldAbortEvaluation(tick, resultantPosition)) collectedDistFromTargetAndBot[fallingCommand] =
                        new Tuple<double, double>(DistanceFromTarget(resultantPosition, targetCollectablePoint), DistanceFromHero(resultantPosition, botStatus));
            }
        }

        private static double DistanceFromTarget(Tuple<int, int> resultantPosition, Tuple<int, int> targetCollectablePoint)
        {
            if (targetCollectablePoint == null) return double.MaxValue;
            return Math.Sqrt(Math.Pow(targetCollectablePoint.Item1 - resultantPosition.Item1, 2) + Math.Pow(targetCollectablePoint.Item2 - resultantPosition.Item2, 2));
        }

        private static bool CanCollect(Tuple<int, int> resultantPosition)
        {
            foreach (var point in BoundingBox(resultantPosition))
            {
                if (point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99) return false;

                if (WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Collectible)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ShouldAbortEvaluation(int overallTicksEvaluated, Tuple<int, int> resultantPosition)
        {
            return overallTicksEvaluated > 16 || BotOnHazard(resultantPosition) || BotInUnachievablePosition(resultantPosition) || BotOutOfBounds(resultantPosition);
        }

        private static bool BotOnPlatform(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var platformsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Platform);

            return platformsBelow;
        }

        private static bool BotOnLadder(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var laddersBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Ladder);

            return laddersBelow;
        }

        private static bool BotContainsLadder(Tuple<int, int> resultantPosition)
        {
            return !BotOutOfBounds(resultantPosition) && BoundingBox(resultantPosition).Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Ladder);
        }


        private static bool BotOnSolid(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var solidsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Solid);

            return solidsBelow;
        }

        private static bool BotOutOfBounds(Tuple<int, int> resultantPosition)
        {
            return BoundingBox(resultantPosition).Any(point => point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99);
        }

        private static bool BotInUnachievablePosition(Tuple<int, int> resultantPosition)
        {
            return BotOutOfBounds(resultantPosition) || BoundingBox(resultantPosition).Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Solid);
        }

        private static bool BotOnHazard(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;
            var hazardsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Hazard);

            return hazardsBelow || BoundingBox(resultantPosition).Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Hazard);
        }

        private static Point[] BoundingBox(Tuple<int, int> resultantPosition) => new System.Drawing.Point[]
            {
            new Point(resultantPosition.Item1, resultantPosition.Item2),
            new Point(resultantPosition.Item1, resultantPosition.Item2 + 1),
            new Point(resultantPosition.Item1 + 1, resultantPosition.Item2),
            new Point(resultantPosition.Item1 + 1, resultantPosition.Item2 + 1)
            };

        private static Tuple<int, int> EvaluateResultantPosition(InputCommand nextCommand, BotStateDTO botStateDTO)
        {
            var resultantPosition = new Tuple<int, int>(botStateDTO.X, botStateDTO.Y);
            var dx = 0;
            var dy = 0;
            var maxEvaluationTicks = 16;
            var dxRevertTick = 16;
            var dyRevertTick = 16;
            switch (nextCommand)
            {
                //TODO Take into account previous command
                case InputCommand.UP:
                    dy = 1;
                    dyRevertTick = HeroOnPlatform(botStateDTO) ? 3 : 1;
                    dxRevertTick = HeroOnPlatform(botStateDTO) ? 16 : 1;
                    break;
                case InputCommand.UPLEFT:
                    dx = -1;
                    dy = 1;
                    dyRevertTick = HeroOnPlatform(botStateDTO) ? 3 : 1;
                    dxRevertTick = HeroOnPlatform(botStateDTO) ? 16 : 1;
                    maxEvaluationTicks = 16;
                    break;
                case InputCommand.UPRIGHT:
                    dx = 1;
                    dy = 1;
                    dyRevertTick = HeroOnPlatform(botStateDTO) ? 3 : 1;
                    dxRevertTick = HeroOnPlatform(botStateDTO) ? 16 : 1;
                    maxEvaluationTicks = 16;
                    break;
                case InputCommand.DOWNLEFT:
                    dx = -1;
                    dy = -1;
                    dxRevertTick = 1;
                    break;
                case InputCommand.DOWNRIGHT:
                    dx = 1;
                    dy = -1;
                    dxRevertTick = 1;
                    break;
                case InputCommand.DIGDOWN:
                    dy = -1;
                    break;
                case InputCommand.DIGLEFT:
                    dx = -1;
                    dyRevertTick = 1;
                    break;
                case InputCommand.DIGRIGHT:
                    dx = 1;
                    dyRevertTick = 1;
                    break;
            }

            //TODO Consider when map is smaller and up restricted by solids
            for (int tick = 1; tick <= 16; tick++)
            {
                if (tick > dxRevertTick) dx = 0;
                if (tick > dyRevertTick) dy = -1;

                resultantPosition = new Tuple<int, int>(resultantPosition.Item1 + dx, resultantPosition.Item2 + dy);



                if (tick > maxEvaluationTicks)
                {
                    return resultantPosition;
                }

                if (HeroOnPlatform(resultantPosition) || HeroOnLadder(resultantPosition))
                {
                    return resultantPosition;
                }
            }
            return resultantPosition;
        }


        private static List<InputCommand> NextSafeCommands(BotStateDTO botStateDTO, List<List<InputCommand>> targetCollectablePermutations, EvaluationMode evaluationMode)
        {
            var safePermutations = new List<PermutationEvaluator>();
            var permutationsToIgnore = new List<List<InputCommand>>();
            foreach (var permutation in targetCollectablePermutations)
            {
                if (permutationsToIgnore.Contains(permutation)) continue;
                var permutationEvaluator = new PermutationEvaluator(permutation, botStateDTO, targetCollectablePoint, lastCommandSent);
                permutationEvaluator.Evaluate(evaluationMode);
                if (permutationEvaluator.FailedValidation)
                {
                    if (permutationEvaluator.FailedValidationAtCommandIndex == 0)
                    {
                        permutationsToIgnore.AddRange(targetCollectablePermutations.Where(x => x[0] == permutation[0]));
                    }

                    if (permutationEvaluator.FailedValidationAtCommandIndex == 1)
                    {
                        permutationsToIgnore.AddRange(targetCollectablePermutations.Where(x => x[0] == permutation[0] && x[1] == permutation[1]));
                    }

                }
                else
                {
                    safePermutations.Add(permutationEvaluator);
                }
            }

            if (safePermutations.Any())
            {
                var orderedList = safePermutations.OrderByDescending(x => x.OverallCollectables).ThenBy(x => x.ResultantDistanceFromTarget).ThenBy(x => x.OverallTicksEvaluated).ToList();
                return orderedList.First().SafeCommands;
            }

            return new List<InputCommand>();
        }

        private static bool HeroOnPlatform(BotStateDTO botStateDTO)
        {
            if (botStateDTO.Y == 0) return false;
            var onPlatform = WorldMapPerspective.ObjectCoordinates[botStateDTO.X, botStateDTO.Y - 1] == ObjectType.Platform || WorldMapPerspective.ObjectCoordinates[botStateDTO.X + 1, botStateDTO.Y - 1] == ObjectType.Platform;
            return onPlatform;
        }

        private static bool HeroOnAirOrCollectable(BotStateDTO botStateDTO)
        {
            if (botStateDTO.Y == 0) return false;
            var onAirOrCollectable = (WorldMapPerspective.ObjectCoordinates[botStateDTO.X, botStateDTO.Y - 1] == ObjectType.Air || WorldMapPerspective.ObjectCoordinates[botStateDTO.X, botStateDTO.Y - 1] == ObjectType.Collectible) && (WorldMapPerspective.ObjectCoordinates[botStateDTO.X + 1, botStateDTO.Y - 1] == ObjectType.Air || WorldMapPerspective.ObjectCoordinates[botStateDTO.X + 1, botStateDTO.Y - 1] == ObjectType.Collectible);
            return onAirOrCollectable;
        }

        private static bool HeroOnLadder(BotStateDTO botStateDTO)
        {
            if (botStateDTO.Y == 0 || BotOutOfBounds(new Tuple<int, int>(botStateDTO.X, botStateDTO.Y))) return false;
            var onPlatform = WorldMapPerspective.ObjectCoordinates[botStateDTO.X, botStateDTO.Y - 1] == ObjectType.Ladder || WorldMapPerspective.ObjectCoordinates[botStateDTO.X + 1, botStateDTO.Y - 1] == ObjectType.Ladder;
            return onPlatform;
        }

        private static bool HeroOnLadder(Tuple<int, int> resultantPosition)
        {
            if (resultantPosition.Item2 == 0 || BotOutOfBounds(resultantPosition)) return false;
            var onPlatform = WorldMapPerspective.ObjectCoordinates[resultantPosition.Item1, resultantPosition.Item2 - 1] == ObjectType.Ladder || WorldMapPerspective.ObjectCoordinates[resultantPosition.Item1 + 1, resultantPosition.Item2 - 1] == ObjectType.Ladder;
            return onPlatform;
        }

        private static bool HeroOnPlatform(Tuple<int, int> resultantPosition)
        {
            if (resultantPosition.Item2 == 0 || BotOutOfBounds(resultantPosition)) return false;

            var onPlatform = WorldMapPerspective.ObjectCoordinates[resultantPosition.Item1, resultantPosition.Item2 - 1] == ObjectType.Platform || WorldMapPerspective.ObjectCoordinates[resultantPosition.Item1 + 1, resultantPosition.Item2 - 1] == ObjectType.Platform;
            return onPlatform;
        }


        private static void UpdateNearestCollectablePoint(BotStateDTO botStateDTO)
        {
            if (!collectiblePoints.Any())
            {
                targetCollectablePoint = null;
                return;
            }

            targetCollectablePoint = collectiblePoints.OrderBy(x => DistanceFromHero(x, botStateDTO)).First();
        }

        private static double DistanceFromHero(Tuple<int, int> point, BotStateDTO botStateDTO)
        {
            return Math.Sqrt(Math.Pow(botStateDTO.X - point.Item1, 2) + Math.Pow(botStateDTO.Y - point.Item2, 2));
        }

        private static void UpdatePerspectives(BotStateDTO botStateDTO)
        {
            if (botStateDTO.CurrentLevel != level)
            {
                nextCommands.Clear();
                collectiblePoints.Clear();
                targetCollectablePoint = null;

                level = botStateDTO.CurrentLevel;
                for (int i = 0; i < 100; i++)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        WorldMapPerspective.SetCoordinates(i, j, (int)ObjectType.Air);
                    }
                }
            }

            for (int i = 0; i < botStateDTO.HeroWindow.Length; i++)
            {
                for (int j = 0; j < botStateDTO.HeroWindow[i].Length; j++)
                {
                    var worldX = botStateDTO.X + i - 16;
                    var worldY = botStateDTO.Y + j - 10;
                    if (worldX >= 0 && worldX < 100 && worldY >= 0 && worldY < 100)
                    {
                        WorldMapPerspective.SetCoordinates(worldX, worldY, (ObjectType)botStateDTO.HeroWindow[i][j]);

                        if (collectiblePoints.Contains(new Tuple<int, int>(worldX, worldY)) && (ObjectType)botStateDTO.HeroWindow[i][j] != ObjectType.Collectible)
                        {
                            collectiblePoints.Remove(new Tuple<int, int>(worldX, worldY));
                        }

                        if ((ObjectType)botStateDTO.HeroWindow[i][j] == ObjectType.Collectible && !collectiblePoints.Contains(new Tuple<int, int>(worldX, worldY)))
                        {
                            collectiblePoints.Add(new Tuple<int, int>(worldX, worldY));
                        }
                    }
                }
            }
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            Array values = Enum.GetValues(typeof(InputCommand));
            Random random = new Random();
            InputCommand inputCommand = (InputCommand)values.GetValue(random.Next(values.Length));

            var botCommand = new BotCommand
            {
                Action = inputCommand,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }

        private void UpLeftCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.UPLEFT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void UpCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.UP,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void UpRightCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.UPRIGHT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void LeftCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.LEFT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void RightCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.RIGHT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DownLeftCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DOWNLEFT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DownRightCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DOWNRIGHT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DownCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DOWN,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DigLeftCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DIGLEFT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DigRightCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DIGRIGHT,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }
        private void DigDownCommand(object sender, EventArgs e)
        {
            var botCommand = new BotCommand
            {
                Action = InputCommand.DIGDOWN,
                BotId = Id
            };

            connection.InvokeAsync("SendPlayerCommand", botCommand);
        }

        static List<List<InputCommand>> GeneratePermutations(List<InputCommand> commands, int length)
        {
            List<List<InputCommand>> permutations = new List<List<InputCommand>>();

            int[] indices = new int[length];
            int commandCount = commands.Count;

            while (true)
            {
                List<InputCommand> currentPermutation = new List<InputCommand>();
                for (int i = 0; i < length; i++)
                {
                    currentPermutation.Add(commands[indices[i]]);
                }
                permutations.Add(currentPermutation);

                int nextIndex = length - 1;
                while (nextIndex >= 0 && indices[nextIndex] == commandCount - 1)
                {
                    nextIndex--;
                }

                if (nextIndex < 0)
                {
                    break;
                }

                indices[nextIndex]++;
                for (int i = nextIndex + 1; i < length; i++)
                {
                    indices[i] = 0;
                }
            }

            return permutations;
        }
    }

    internal class PermutationEvaluator
    {
        private List<InputCommand> permutation { get; }
        private BotStateDTO botStateDTO;
        private Tuple<int, int> targetCollectablePoint;
        private InputCommand lastCommandSent;
        private bool targetCollected = false;
        private List<Tuple<int, int>> collectedPositions = new List<Tuple<int, int>>();
        private int overallTicksEvaluated;

        public PermutationEvaluator(List<InputCommand> permutation, BotStateDTO botStateDTO, Tuple<int, int> targetCollectablePoint, InputCommand lastCommandSent)
        {
            this.permutation = permutation;
            this.botStateDTO = botStateDTO;
            this.targetCollectablePoint = targetCollectablePoint;
            this.lastCommandSent = lastCommandSent;
        }

        public void Evaluate(EvaluationMode evaluationMode)
        {
            ResultantPosition = new Tuple<int, int>(botStateDTO.X, botStateDTO.Y);
            var commandIndex = 0;
            InputCommand evaluatingCommand = InputCommand.None;
            var dx = 0;
            var dy = 0;
            var resultantState = botStateDTO.CurrentState;
            foreach (var inputCommand in permutation)
            {
                var maxEvaluationTicks = 16;
                var dxRevertTick = 16;
                var dyRevertTick = 16;
                var dugInDirection = false;
                switch (inputCommand)
                {
                    case InputCommand.UP:
                        //if (lastCommandSent == InputCommand.UPRIGHT || lastCommandSent == InputCommand.RIGHT || lastCommandSent == InputCommand.UPLEFT || lastCommandSent == InputCommand.LEFT)
                        //{
                        //    FailedValidation = true;
                        //    FailedValidationAtCommandIndex = commandIndex;
                        //    return;

                        //    //dx =  1;
                        //    //evaluatingCommand = InputCommand.UPRIGHT;
                        //}else if(lastCommandSent == InputCommand.UPLEFT)
                        //{
                        //    FailedValidation = true;
                        //    FailedValidationAtCommandIndex = commandIndex;
                        //    return;

                        //    //dx = -1;
                        //    //evaluatingCommand = InputCommand.UPLEFT;
                        //}
                        //else
                        //{
                        //    dx = 0;
                        //    evaluatingCommand = InputCommand.UP;
                        //}
                        dx = 0;
                        evaluatingCommand = InputCommand.UP;

                        dy = 1;
                        if (BotOnPlatform())
                        {
                            resultantState = "Jumping";
                            dyRevertTick = 3;
                            dxRevertTick = 16;
                        }

                        if (BotContainsLadder())
                        {
                            resultantState = "Moving";
                            dyRevertTick = 1;
                            dxRevertTick = 1;
                        }

                        break;
                    case InputCommand.UPLEFT:
                        dx = -1;
                        dy = 1;
                        if (BotOnPlatform())
                        {
                            resultantState = "Jumping";
                            dyRevertTick = 3;
                            dxRevertTick = 16;
                        }

                        if (BotContainsLadder())
                        {
                            resultantState = "Moving";
                            dyRevertTick = 1;
                            dxRevertTick = 1;
                        }
                        maxEvaluationTicks = 16;
                        evaluatingCommand = InputCommand.UPLEFT;
                        break;
                    case InputCommand.UPRIGHT:
                        dx = 1;
                        dy = 1;
                        if (BotOnPlatform())
                        {
                            resultantState = "Jumping";
                            dyRevertTick = 3;
                            dxRevertTick = 16;
                        }

                        if (BotContainsLadder())
                        {
                            resultantState = "Moving";
                            dyRevertTick = 1;
                            dxRevertTick = 1;
                        }
                        maxEvaluationTicks = 16;
                        evaluatingCommand = InputCommand.UPRIGHT;
                        break;
                    case InputCommand.DOWNLEFT:
                        dx = -1;
                        dy = -1;
                        dxRevertTick = 1;
                        evaluatingCommand = InputCommand.DOWNLEFT;
                        break;
                    case InputCommand.DOWNRIGHT:
                        dx = 1;
                        dy = -1;
                        dxRevertTick = 1;
                        evaluatingCommand = InputCommand.DOWNRIGHT;
                        break;
                    case InputCommand.DIGDOWN:
                        dy = -1;
                        dugInDirection = true;
                        evaluatingCommand = InputCommand.DIGDOWN;
                        break;
                    case InputCommand.DIGLEFT:
                        dugInDirection = true;
                        dx = -1;
                        dy = 0;
                        dxRevertTick = 1;
                        //dyRevertTick = 1;

                        evaluatingCommand = InputCommand.DIGLEFT;
                        break;
                    case InputCommand.DIGRIGHT:
                        dugInDirection = true;
                        dx = 1;
                        dy = 0;
                        dxRevertTick = 1;
                        //dyRevertTick = 1;

                        evaluatingCommand = InputCommand.DIGRIGHT;
                        break;
                    case InputCommand.LEFT:
                        dx = -1;
                        dy = resultantState == "Falling" ? -1 : 0;
                        dxRevertTick = resultantState == "Falling" ? 16 : 1;
                        break;
                    case InputCommand.RIGHT:
                        dx = 1;
                        dy = resultantState == "Falling" ? -1 : 0;
                        dxRevertTick = resultantState == "Falling" ? 16 : 1;
                        break;
                }

                //TODO Consider when map is smaller and up restricted by solids
                for (int tick = 1; tick <= 16; tick++)
                {
                    overallTicksEvaluated++;

                    if (tick > dxRevertTick) dx = 0;
                    if (tick > dyRevertTick && resultantState == "Jumping")
                    {
                        resultantState = "Falling";
                        dy = -1;
                    }

                    ResultantPosition = new Tuple<int, int>(ResultantPosition.Item1 + dx, ResultantPosition.Item2 + dy);


                    TryCollect();

                    if (evaluationMode == EvaluationMode.Collect && collectedPositions.Any() && (evaluatingCommand == InputCommand.UPRIGHT || evaluatingCommand == InputCommand.UPLEFT) && tick > dyRevertTick)
                    {
                        SafeCommands.Add(evaluatingCommand);
                        break;
                    }

                    if (overallTicksEvaluated > maxEvaluationTicks || BotOnHazard() || BotInUnachievablePosition(dugInDirection) || BotOutOfBounds()/* || BotOnSolid()*/)
                    {
                        FailedValidation = true;
                        FailedValidationAtCommandIndex = commandIndex;
                        return;
                    }

                    if (BotOnSolid())
                    {
                        if (evaluationMode == EvaluationMode.Collect)
                        {
                            if (!collectedPositions.Any())
                            {
                                FailedValidation = true;
                                return;
                            }
                        }
                        SafeCommands.Add(evaluatingCommand);
                        break;
                    }

                    if (BotContainsLadder())
                    {
                        if (evaluationMode == EvaluationMode.Collect)
                        {
                            if (!collectedPositions.Any())
                            {
                                FailedValidation = true;
                                return;
                            }
                        }
                        SafeCommands.Add(evaluatingCommand);
                        break;
                    }

                    if (BotOnPlatform())
                    {
                        if (evaluationMode == EvaluationMode.Collect)
                        {
                            if (!collectedPositions.Any())
                            {
                                FailedValidation = true;
                                return;
                            }
                        }
                        SafeCommands.Add(evaluatingCommand);
                        break;
                    }

                    if (BotOnLadder())
                    {
                        if (resultantState == "Jumping" && (evaluatingCommand == InputCommand.UPLEFT || evaluatingCommand == InputCommand.UPRIGHT || (evaluatingCommand == InputCommand.UP)))
                        {
                            continue;
                        }

                        if (evaluationMode == EvaluationMode.Collect)
                        {
                            if (!collectedPositions.Any())
                            {
                                FailedValidation = true;
                                return;
                            }
                        }
                        SafeCommands.Add(evaluatingCommand);
                        break;
                    }

                    dy = resultantState == "Jumping" ? dy : -1;
                    resultantState = "Falling";
                    //var resultantPosition = new Tuple<int, int>(ResultantPosition.Item1, ResultantPosition.Item2);
                    //var bestSafeLanding = TryLandSafely(resultantPosition, tick, dx, fallingPermutations, evaluationMode);

                    //if (bestSafeLanding != null)
                    //{
                    //    collectedPositions.AddRange(bestSafeLanding.Item2);
                    //    if (bestSafeLanding.Item1.Any())
                    //    {
                    //        if (evaluationMode == EvaluationMode.Collect)
                    //        {
                    //            if (collectedPositions.Any())
                    //            {
                    //                FailedValidation = true;
                    //                FailedValidationAtCommandIndex = commandIndex;
                    //            }
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    FailedValidation = true;
                    //    FailedValidationAtCommandIndex = commandIndex;
                    //}
                }
                lastCommandSent = inputCommand;
                commandIndex++;
            }
            if (BotOnAirOrCollectable())
            {
                FailedValidation = true;
                FailedValidationAtCommandIndex = permutation.Count - 1;
            }
        }

        private Tuple<List<InputCommand>, List<Tuple<int, int>>> TryLandSafely(Tuple<int, int> resultantPosition, int tick, int dx, List<List<InputCommand>> fallingPermutations, EvaluationMode evaluationMode)
        {
            var safeLandingEvaluations = new List<Tuple<List<InputCommand>, List<Tuple<int, int>>>>();
            foreach (var fallingPermutation in fallingPermutations)
            {
                safeLandingEvaluations.Add(EvaluateFallingPermutation(resultantPosition, tick, dx, fallingPermutation));
            }

            safeLandingEvaluations = safeLandingEvaluations.Where(x => x.Item1.Any() && x.Item2.Count >= (evaluationMode == EvaluationMode.Collect ? 1 : 0)).ToList();

            return safeLandingEvaluations.OrderByDescending(x => x.Item2.Count).ThenByDescending(x => x.Item1.Count).FirstOrDefault();
        }

        private Tuple<List<InputCommand>, List<Tuple<int, int>>> EvaluateFallingPermutation(Tuple<int, int> resultantPosition, int tick, int deltaX, List<InputCommand> fallingPermutation)
        {
            var safeLandingEvaluation = new Tuple<List<InputCommand>, List<Tuple<int, int>>>(new List<InputCommand>(), new List<Tuple<int, int>>());
            List<Tuple<int, int>> localCollectedPositions = new List<Tuple<int, int>>();
            var overallTicksEvaluated = OverallTicksEvaluated;

            foreach (var inputCommand in fallingPermutation)
            {
                deltaX = GetDeltaX(inputCommand);

                resultantPosition = new Tuple<int, int>(resultantPosition.Item1 + deltaX, resultantPosition.Item2 - 1);
                TryCollect(resultantPosition, localCollectedPositions);

                if (ShouldAbortEvaluation(overallTicksEvaluated, resultantPosition))
                {
                    return new Tuple<List<InputCommand>, List<Tuple<int, int>>>(new List<InputCommand>(), new List<Tuple<int, int>>());
                }

                safeLandingEvaluation.Item1.Add(inputCommand);

                if (IsOnPlatformOrLadder(resultantPosition))
                {
                    safeLandingEvaluation.Item2.AddRange(localCollectedPositions);
                    return safeLandingEvaluation;
                }

                overallTicksEvaluated++;
                tick++;
            }

            for (int i = tick; i <= 17; i++)
            {
                resultantPosition = new Tuple<int, int>(resultantPosition.Item1 + deltaX, resultantPosition.Item2 - 1);
                TryCollect(resultantPosition, localCollectedPositions);

                if (ShouldAbortEvaluation(overallTicksEvaluated, resultantPosition))
                {
                    return new Tuple<List<InputCommand>, List<Tuple<int, int>>>(new List<InputCommand>(), new List<Tuple<int, int>>());
                }

                if (IsOnPlatformOrLadder(resultantPosition))
                {
                    safeLandingEvaluation.Item2.AddRange(localCollectedPositions);
                    return safeLandingEvaluation;
                }

                overallTicksEvaluated++;
                i++;
            }

            return safeLandingEvaluation;
        }

        private int GetDeltaX(InputCommand inputCommand)
        {
            return inputCommand == InputCommand.LEFT ? -1 : 1;
        }

        private bool ShouldAbortEvaluation(int overallTicksEvaluated, Tuple<int, int> resultantPosition)
        {
            return overallTicksEvaluated > 16 || BotOnHazard(resultantPosition) || BotInUnachievablePosition(resultantPosition) || BotOutOfBounds(resultantPosition) || BotOnSolid(resultantPosition);
        }

        private bool IsOnPlatformOrLadder(Tuple<int, int> resultantPosition)
        {
            return BotOnPlatform(resultantPosition) || BotOnLadder(resultantPosition);
        }


        private bool BotOnAirOrCollectable()
        {
            if (ResultantPosition.Item2 == 0 || BotOutOfBounds()) return false;

            var onAirOrCollectable = (WorldMapPerspective.ObjectCoordinates[ResultantPosition.Item1, ResultantPosition.Item2 - 1] == ObjectType.Air || WorldMapPerspective.ObjectCoordinates[ResultantPosition.Item1, ResultantPosition.Item2 - 1] == ObjectType.Collectible) && (WorldMapPerspective.ObjectCoordinates[ResultantPosition.Item1 + 1, ResultantPosition.Item2 - 1] == ObjectType.Air || WorldMapPerspective.ObjectCoordinates[ResultantPosition.Item1 + 1, ResultantPosition.Item2 - 1] == ObjectType.Collectible);
            return onAirOrCollectable;
        }

        private bool BotOnLadder()
        {
            var bottomRow = BoundingBox().Where(pos => pos.Y == ResultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds()) return false;

            var laddersBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Ladder);

            return laddersBelow;
        }

        private bool BotContainsLadder()
        {
            return !BotOutOfBounds() && BoundingBox().Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Ladder);
        }

        private bool BotOnLadder(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var laddersBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Ladder);

            return laddersBelow;
        }

        private bool BotOnPlatform()
        {
            var bottomRow = BoundingBox().Where(pos => pos.Y == ResultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds()) return false;

            var platformsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Platform);

            return platformsBelow;
        }

        private bool BotOnPlatform(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var platformsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Platform);

            return platformsBelow;
        }

        private bool BotOnSolid(Tuple<int, int> resultantPosition)
        {
            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;

            var solidsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Solid);

            return solidsBelow;
        }

        private bool BotOnSolid()
        {
            var bottomRow = BoundingBox().Where(pos => pos.Y == ResultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds()) return false;

            var solidsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Solid);

            return solidsBelow;
        }

        private bool BotOnHazard()
        {

            var bottomRow = BoundingBox().Where(pos => pos.Y == ResultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds()) return false;
            var hazardsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Hazard);

            return hazardsBelow || BoundingBox().Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Hazard);
        }

        private bool BotOnHazard(Tuple<int, int> resultantPosition)
        {

            var bottomRow = BoundingBox(resultantPosition).Where(pos => pos.Y == resultantPosition.Item2);
            if (bottomRow.Any(x => x.Y == 0) || BotOutOfBounds(resultantPosition)) return false;
            var hazardsBelow = bottomRow.Any(pos => WorldMapPerspective.ObjectCoordinates[pos.X, pos.Y - 1] == ObjectType.Hazard);

            return hazardsBelow || BoundingBox(resultantPosition).Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Hazard);
        }

        private bool BotInUnachievablePosition(bool dugInDirection)
        {
            return BotOutOfBounds() || BoundingBox().Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Solid && !dugInDirection);
        }

        private bool BotInUnachievablePosition(Tuple<int, int> resultantPosition)
        {
            return BotOutOfBounds(resultantPosition) || BoundingBox(resultantPosition).Any(point => WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Solid);
        }

        private bool BotOutOfBounds()
        {
            return BoundingBox().Any(point => point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99);
        }

        private bool BotOutOfBounds(Tuple<int, int> resultantPosition)
        {
            return BoundingBox(resultantPosition).Any(point => point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99);
        }

        private void TryCollect()
        {
            foreach (var point in BoundingBox())
            {
                if (point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99) return;

                if (WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Collectible)
                {
                    var collectedPosition = new Tuple<int, int>(point.X, point.Y);
                    if (collectedPosition.Item1 == targetCollectablePoint.Item1 && collectedPosition.Item2 == targetCollectablePoint.Item2) targetCollected = true;
                    if (!collectedPositions.Contains(collectedPosition)) collectedPositions.Add(collectedPosition);
                }
            }
        }

        private void TryCollect(Tuple<int, int> resultantPosition, List<Tuple<int, int>> localCollectedPositions)
        {
            foreach (var point in BoundingBox(resultantPosition))
            {
                if (point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99) return;

                if (WorldMapPerspective.ObjectCoordinates[point.X, point.Y] == ObjectType.Collectible)
                {
                    var collectedPosition = new Tuple<int, int>(point.X, point.Y);
                    if (!localCollectedPositions.Contains(collectedPosition)) localCollectedPositions.Add(collectedPosition);
                }
            }
        }

        public bool FailedValidation { get; set; }
        public int FailedValidationAtCommandIndex { get; set; }
        public int OverallTicksEvaluated { get => overallTicksEvaluated; }
        public int OverallCollectables { get => collectedPositions.Count; }
        public Tuple<int, int> ResultantPosition { get; set; }
        public double ResultantDistanceFromTarget { get => targetCollected ? 0 : DistanceBetweenPoints(ResultantPosition, targetCollectablePoint); }
        public List<InputCommand> SafeCommands { get; } = new List<InputCommand>();

        private double DistanceBetweenPoints(Tuple<int, int> point, Tuple<int, int> targetPoint)
        {
            return Math.Sqrt(Math.Pow(targetPoint.Item1 - point.Item1, 2) + Math.Pow(targetPoint.Item2 - point.Item2, 2));
        }
        private Point[] BoundingBox() => new Point[]
            {
            new Point(ResultantPosition.Item1, ResultantPosition.Item2),
            new Point(ResultantPosition.Item1, ResultantPosition.Item2 + 1),
            new Point(ResultantPosition.Item1 + 1, ResultantPosition.Item2),
            new Point(ResultantPosition.Item1 + 1, ResultantPosition.Item2 + 1)
            };
        private Point[] BoundingBox(Tuple<int, int> resultantPosition) => new Point[]
            {
            new Point(resultantPosition.Item1, resultantPosition.Item2),
            new Point(resultantPosition.Item1, resultantPosition.Item2 + 1),
            new Point(resultantPosition.Item1 + 1, resultantPosition.Item2),
            new Point(resultantPosition.Item1 + 1, resultantPosition.Item2 + 1)
            };
    }
}