﻿using Domain.Enums;
using Domain.Models;
using Domain.Models.Pathfinding;
using System;
using System.Collections.Generic;

namespace ReferenceBot.Services.Interfaces
{
    public interface IPathTraversalService
    {
        Path CurrentPath { get; }
        Guid BotId { get; }
        BotCommand NextCommand(BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict);
        void SetBotId(Guid id);
    }
}
