using Domain.Enums;
using Domain.Models;
using Domain.Models.Pathfinding;
using System;
using System.Collections.Generic;

namespace ReferenceBot.Services.Interfaces
{
    public interface IPathfindingService
    {
        bool Busy { get; }

        event EventHandler<Path> BestPathFound;
        Path FindBestPath(Point startPosition, Point deltaToStartPosition, MovementState botMovementStateAtStart, int jumpHeightAtStart, PathType pathType, bool clearExcludedPoints);
    }
}
