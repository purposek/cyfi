using Domain.Enums;
using Domain.Models;
using System;
using ReferenceBot.AI.DataStructures.Pathfinding;
using System.Linq;
using System.Collections.Generic;
using C5;

namespace ReferenceBot.AI.States
{

    class Collecting : State
    {
        Point Collectible;
        Path PathToCollectible;
        bool Exploring;

        public Collecting(BotStateMachine _stateMachine, Point collectible, Path pathToCollectible, bool exploring) : base(_stateMachine)
        {
            Collectible = collectible;
            PathToCollectible = pathToCollectible;
            Exploring = exploring;
        }

        public int ChangeStateCooldown { get; private set; }

        public override void EnterState(State PreviousState)
        {
            Console.WriteLine("Entered Collecting");
        }

        public override void ExitState(State NextState)
        {
        }

        public override InputCommand Update(BotStateDTO BotState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            var gameTickToCheck = BotState.GameTick;
            var stagnant = 0;
            while (ChangeStateCooldown < 0 && stagnant < 3 && gameStateDict.ContainsKey(gameTickToCheck - 1) && gameStateDict[gameTickToCheck - 1].Position.Equals(gameStateDict[gameTickToCheck].Position))
            {
                stagnant++;
                gameTickToCheck--;
            }

            var nextNode = PathToCollectible.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(BotState.CurrentPosition));
           if((!Exploring && !WorldMapPerspective.Collectibles.Contains(Collectible)) || stagnant > 2 || nextNode == null || WorldMapPerspective.BotBoundsContainPoint(BotState.CurrentPosition, Collectible)) 
            {
                ChangeStateCooldown = 1;
                ChangeState(new SearchingState(StateMachine));
                return InputCommand.None;
            }

            ChangeStateCooldown--;
            return nextNode.CommandToReachMe;
        }
    }
}
