using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Peasants
{
    public class CharacterSelectorVM : ViewModel
    {
        public MBBindingList<CharacterCard> Cards { get; set; }

        public CharacterSelectorVM(List<Hero> heroes)
        {
            Cards = new MBBindingList<CharacterCard>();

            if (heroes == null) return;

            foreach (Hero hero in heroes)
            {
                try
                {
                    if (hero == null) continue;
                    Cards.Add(new CharacterCard(hero));
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Card creation error: " + ex.Message, Colors.Red));
                }
            }
        }

        public void Close()
        {
            try
            {
                // Игрок закрыл окно без выбора (крестик/Esc).
                // Удаляем всех кандидатов, которых только что создали.
                // null означает "удалить всех" — см. CleanupUnusedCandidates.
                PeasantsBehavior.CleanupUnusedCandidates(null);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] Close/cleanup error: " + ex.Message, Colors.Red));
            }
            finally
            {
                try { PeasantsBehavior.DeleteVMLayer(); }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Close/DeleteVMLayer error: " + ex.Message, Colors.Red));
                }
            }
        }
    }
}