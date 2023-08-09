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
        private readonly Dictionary<Point, InputCommand> subsequentBestInputCommandFromPoint = new();
        private Guid botId;
        private Path currentPath;
        private Path subsequentBestPath;

        public Path CurrentPath => currentPath;

        public PathTraversalService(IPathfindingService pathfindingService)
        {
            this.pathfindingService = pathfindingService;
            this.pathfindingService.BestPathFound += OnBestPathFound;
            this.pathfindingService.SubsequentBestPathFound += OnSubsequentBestPathFound;
        }

        public BotCommand NextCommand(BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            var gameTickToCheck = botState.GameTick;
            var stagnant = 0;
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

            if (currentPath != null && botState.CurrentPosition.Equals(currentPath.Target) && subsequentBestInputCommandFromPoint.Keys.Any(x => x.Equals(botState.CurrentPosition)))
            {
                bestInputCommandFromPoint.Clear();
                currentPath = new Path(subsequentBestPath);
                foreach (var kvp in subsequentBestInputCommandFromPoint)
                {
                    bestInputCommandFromPoint.Add(kvp.Key, kvp.Value);
                }

                return new BotCommand
                {
                    BotId = botId,
                    Action = bestInputCommandFromPoint[botState.CurrentPosition]
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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("***********GOT HERE**************");
            Console.ResetColor();
            return new BotCommand
            {
                BotId = botId,
                Action = InputCommand.None
            };
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

        private void OnSubsequentBestPathFound(object sender, Path subsequentPath)
        {
            subsequentBestInputCommandFromPoint.Clear();
            subsequentBestPath = subsequentPath;
            foreach (var node in subsequentPath.Nodes)
            {
                var nextNode = subsequentPath.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(node));
                if (nextNode != null)
                {
                    subsequentBestInputCommandFromPoint[node] = nextNode.CommandToReachMe;
                }
            }

        }

    }
}
