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
            foreach (Hero hero in heroes)
            {
                Cards.Add(new CharacterCard(hero));
            }
        }

        public void Close()
        {
            // Игрок закрыл окно без выбора (крестик/Esc).
            // Удаляем всех кандидатов, которых только что создали.
            // null означает "удалить всех" — см. CleanupUnusedCandidates.
            PeasantsBehavior.CleanupUnusedCandidates(null);
            PeasantsBehavior.DeleteVMLayer();
        }
    }
}