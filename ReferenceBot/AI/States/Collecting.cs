using Domain.Enums;
using Domain.Models;
using System;
using ReferenceBot.AI.DataStructures.Pathfinding;
using System.Linq;

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

        public override void EnterState(State PreviousState)
        {
            Console.WriteLine("Entered Collecting");
        }

        public override void ExitState(State NextState)
        {
        }

        public override InputCommand Update(BotStateDTO BotState)
        {
           var nextNode = PathToCollectible.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(BotState.CurrentPosition));
           if((!Exploring && !WorldMapPerspective.Collectibles.Contains(Collectible)) || nextNode == null || WorldMapPerspective.BotBoundsContainPoint(BotState.CurrentPosition, Collectible)) 
            {
                ChangeState(new SearchingState(StateMachine));
                return InputCommand.None;
            }

            return nextNode.CommandToReachMe;
        }
    }
}
