using ReferenceBot.AI;
using Domain.Enums;
using Domain.Models;
using System;
using System.Collections.Generic;

namespace ReferenceBot.Services
{
  class BotService
  {
    private Guid BotId;
    private BotStateMachine BotFSM;
   
    public BotService()
    {
        BotFSM = new();
    }

    public BotCommand ProcessState(BotStateDTO BotState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
    {
      InputCommand ActionToTake = BotFSM.Update(BotState, gameStateDict);
      return new BotCommand
      {
        BotId = BotId,
        Action = ActionToTake,
      };
    }

    public void SetBotId(Guid NewBotId)
    {
      BotId = NewBotId;
    }
  }
}