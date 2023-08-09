using Domain.Enums;
using Domain.Models;
using Domain.Models.Pathfinding;
using System;
using System.Collections.Generic;

namespace ReferenceBot.Services.Interfaces
{
    public interface IPathfindingService
    {
        event EventHandler<Path> BestPathFound;
        event EventHandler<Path> SubsequentBestPathFound;

        void EvaluateSubsequentBestPathFromCurrentTarget(Path currentPath);
        Path FindBestPath(Point startPosition, Point deltaToStartPosition, MovementState botMovementStateAtStart, int jumpHeightAtStart, PathType pathType, bool clearExcludedPoints);
    }
}
