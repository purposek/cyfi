using Domain.Enums;
using Domain.Models;
using Domain.Models.Pathfinding;
using ReferenceBot.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReferenceBot.Services
{
    public class PathTraversalService : IPathTraversalService
    {
        public event EventHandler<(Path, BotStateDTO, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)>)> FindNextBestPath;
        private readonly IPathfindingService pathfindingService;
        private readonly Dictionary<Point, InputCommand> bestInputCommandFromPoint = new();
        private Guid botId;
        private Path currentPath;

        public Path CurrentPath => currentPath;
        public Guid BotId => botId;

        public PathTraversalService(IPathfindingService pathfindingService)
        {
            this.pathfindingService = pathfindingService;
            this.pathfindingService.BestPathFound += OnBestPathFound;
        }

        public BotCommand NextCommand(BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            var gameTickToCheck = botState.GameTick;
            var stagnant = 0;

            if(botState.GameTick > 30 && botState.GameTick % 30 == 0 && PatternFound(PositionHistory.GetLatestPositions())) {
                stagnant = 4;
            }

            while (stagnant < 3 && gameStateDict.ContainsKey(gameTickToCheck - 1) && gameStateDict[gameTickToCheck - 1].Position.Equals(gameStateDict[gameTickToCheck].Position))
            {
                stagnant++;
                gameTickToCheck--;
            }

            InputCommand icommand;
            if (stagnant < 3 && bestInputCommandFromPoint.TryGetValue(botState.CurrentPosition, out InputCommand inputCommand) && ((currentPath.PathType != PathType.Collecting ) || WorldMapPerspective.Collectibles.Contains(currentPath.Target)))
            {
                return new BotCommand
                {
                    BotId = botId,
                    Action = inputCommand
                };
            }

            MovementState botMovementState;
            try
            {
                botMovementState = (MovementState)Enum.Parse(typeof(MovementState), botState.CurrentState);
            }
            catch (ArgumentException)
            {
                botMovementState = MovementState.Idle; // Default value
            }

            var jumpHeight = 0;
            gameTickToCheck = botState.GameTick;
            while (gameStateDict.ContainsKey(gameTickToCheck) && gameStateDict[gameTickToCheck].MovementState == "Jumping")
            {
                jumpHeight++;
                gameTickToCheck--;
            }

            WorldMapPerspective.UpdateState(botState);

            if (!this.pathfindingService.Busy)
            {
                var foundPath = this.pathfindingService.FindBestPath(botState.CurrentPosition, gameStateDict[botState.GameTick].DeltaToPosition, botMovementState, jumpHeight, PathType.Collecting, true);
                if (foundPath != null)
                {
                    var nextNode = foundPath.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(botState.CurrentPosition));
                    if (nextNode != null)
                    {
                        icommand = nextNode.CommandToReachMe;
                    }
                    else
                    {
                        icommand = InputCommand.None;
                    }


                    BotCommand command = new BotCommand
                    {
                        BotId = botId,
                        Action = icommand
                    };
                    return command;
                }
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("***********GOT HERE**************");
            Console.ResetColor();
            return new BotCommand
            {
                BotId = botId,
                Action = InputCommand.None
            };
        }
        private bool PatternFound(List<Point> points)
        {
            for (int patternLength = 2; patternLength <= 10; patternLength++)
            {
                // Iterate through the list up to the last possible pattern
                for (int i = 0; i <= points.Count - patternLength * 2; i++)
                {
                    bool patternFound = true;
                    // Check if the pattern of the current length is repeated
                    for (int j = 0; j < patternLength; j++)
                    {
                        if (!points[i + j].Equals(points[i + j + patternLength]))
                        {
                            patternFound = false;
                            break;
                        }
                    }
                    if (patternFound)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"^^^^^^^^Repetitive pattern of length {patternLength} found starting at index {i}^^^^^^^^");
                        Console.ResetColor();

                        return true; // Return as soon as the first pattern is found
                    }
                }
            }
            return false; // Return false if no pattern is found
        }

        public void SetBotId(Guid id)
        {
            this.botId = id;
        }

        private void OnBestPathFound(object sender, Path bestPath)
        {
            bestInputCommandFromPoint.Clear();
            currentPath = bestPath;
            foreach (var node in bestPath.Nodes)
            {
                var nextNode = bestPath.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(node));
                if (nextNode != null)
                {
                    bestInputCommandFromPoint[node] = nextNode.CommandToReachMe;
                }
            }
        }
    }
}
