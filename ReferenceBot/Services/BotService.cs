using ReferenceBot.AI;
using Domain.Enums;
using Domain.Models;
using System;

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

    public BotCommand ProcessState(BotStateDTO BotState)
    {
      InputCommand ActionToTake = BotFSM.Update(BotState);
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