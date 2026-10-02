using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Peasants
{
    public class SubModule : MBSubModuleBase
    {
        private static readonly List<Action> ActionsToExecuteNextTick = new List<Action>();

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                ((CampaignGameStarter)gameStarterObject).AddBehavior(new PeasantsBehavior());
            }
        }

        public static void ExecuteActionOnNextTick(Action action)
        {
            if (action != null)
            {
                ActionsToExecuteNextTick.Add(action);
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            foreach (Action action in ActionsToExecuteNextTick)
            {
                action();
            }
            ActionsToExecuteNextTick.Clear();
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }
    }
}