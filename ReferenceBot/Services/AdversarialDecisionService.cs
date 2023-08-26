using Domain.Enums;
using Domain.Models;
using ReferenceBot.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ReferenceBot.Services
{
    public class AdversarialDecisionService : IAdversarialDecisionService
    {
        private IPathTraversalService pathTraversalService;

        public AdversarialDecisionService(IPathTraversalService pathTraversalService)
        {
            this.pathTraversalService = pathTraversalService;
        }

        public BotCommand NextCommand(BotCommand command, BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            var radarActive = CommandHistory.Contains(InputCommand.RADAR);
            var evadingOrApproaching = WorldMapPerspective.PerformingAdversarialManouvreToTick > botState.GameTick;


            if (!evadingOrApproaching && botState.CurrentState == "Idle" && ((botState.Collected > 8 || WorldMapPerspective.TicksInLevel > 50)) && !radarActive)
            {
                return new BotCommand
                {
                    BotId = pathTraversalService.BotId,
                    Action = InputCommand.RADAR
                };
            }

            if (!evadingOrApproaching && radarActive)
            {
                InputCommand icommand;
                if (pathTraversalService.NavigateAwayFromOpponentPositions(command, botState, gameStateDict)) { 
                    var nextNode = pathTraversalService.CurrentPath.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(botState.CurrentPosition));
                    if (nextNode != null)
                    {
                        icommand = nextNode.CommandToReachMe;
                    }
                    else
                    {
                        icommand = InputCommand.None;
                    }


                    return new BotCommand
                    {
                        BotId = pathTraversalService.BotId,
                        Action = icommand
                    };
                }
            }

            if (WorldMapPerspective.OpponentsInStealRange && radarActive && botState.RadarData.Any(x => x[1] == 1))
            {
                return new BotCommand
                {
                    BotId = pathTraversalService.BotId,
                    Action = InputCommand.STEAL
                };
            }


            if (!evadingOrApproaching && (radarActive && botState.RadarData.Any(x => x[1] == 1 || x[1] == 2)))
            {
                var target = botState.RadarData.First(y => y[1] == botState.RadarData.Min(x => x[1]));
                var generalDirection = (InputCommand)target[0];
                var generalDistance = target[1];
                return pathTraversalService.NavigateTowardsOpponentPositions(command, botState, gameStateDict, generalDirection, generalDistance);
            }

            return command;
        }
    }
}
