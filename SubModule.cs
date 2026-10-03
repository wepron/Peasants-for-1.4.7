using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
            try
            {
                if (game.GameType is Campaign)
                {
                    ((CampaignGameStarter)gameStarterObject).AddBehavior(new PeasantsBehavior());
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] OnGameStart error: " + ex.Message, Colors.Red));
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

            if (ActionsToExecuteNextTick.Count == 0) return;

            // Снапшот: если во время выполнения action снова добавит что-то
            // в очередь, это выполнится на следующем тике, а не сломает итерацию.
            List<Action> snapshot = ActionsToExecuteNextTick.ToList();
            ActionsToExecuteNextTick.Clear();

            foreach (Action action in snapshot)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Tick action error: " + ex.Message, Colors.Red));
                }
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }
    }
}