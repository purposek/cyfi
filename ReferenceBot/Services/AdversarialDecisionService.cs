using Domain.Enums;
using Domain.Models;
using ReferenceBot.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

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

            if (botState.CurrentState == "Idle" && (WorldMapPerspective.OpponentsInCloseRange || WorldMapPerspective.LevelMinCollectablesForPursuit[botState.CurrentLevel] < botState.Collected) && !radarActive)
            {
                return new BotCommand
                {
                    BotId = pathTraversalService.BotId,
                    Action = InputCommand.RADAR
                };
            }

            //if (WorldMapPerspective.OpponentsInCloseRange && radarActive && !botState.RadarData.Any(x => x[1] == 1))
            //{
            //    return new BotCommand
            //    {
            //        BotId = pathTraversalService.BotId,
            //        Action = InputCommand.EVADE
            //    };
            //}

            if (WorldMapPerspective.OpponentsInStealRange && radarActive && botState.RadarData.Any(x => x[1] == 1))
            {
                return new BotCommand
                {
                    BotId = pathTraversalService.BotId,
                    Action = InputCommand.STEAL
                };
            }

            //if (WorldMapPerspective.LevelMinCollectablesForPursuit[botState.CurrentLevel] < botState.Collected && radarActive && botState.RadarData.Any())
            //{
            //    return new BotCommand
            //    {
            //        BotId = pathTraversalService.BotId,
            //        Action = InputCommand.PURSUE
            //    };
            //}

            return command;
        }
    }
}
