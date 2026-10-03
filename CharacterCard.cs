using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Hero;
using static TaleWorlds.Core.ViewModelCollection.CharacterViewModel;

namespace Peasants
{
    public class CharacterCard : ViewModel
    {
        private CharacterViewModel _unitCharacter;
        private Hero _hero;
        private MBBindingList<EncyclopediaSkillVM> _skills;

        public string Name
        {
            get
            {
                try
                {
                    if (_hero == null) return "?";

                    TextObject text = new TextObject("{NAME}\n(Age : {AGE})", null);
                    text.SetTextVariable("NAME", _hero.Name);
                    text.SetTextVariable("AGE", (int)_hero.Age);
                    return text.ToString();
                }
                catch
                {
                    return "?";
                }
            }
        }

        [DataSourceProperty]
        public CharacterViewModel UnitCharacter
        {
            get => _unitCharacter;
            set
            {
                if (value != _unitCharacter)
                {
                    _unitCharacter = value;
                    OnPropertyChangedWithValue(value, "UnitCharacter");
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<EncyclopediaSkillVM> Skills
        {
            get => _skills;
            set
            {
                if (value != _skills)
                {
                    _skills = value;
                    OnPropertyChangedWithValue(value, "Skills");
                }
            }
        }

        public CharacterCard(Hero hero)
        {
            _hero = hero;
            UnitCharacter = new CharacterViewModel(StanceTypes.None);

            try
            {
                if (_hero?.CharacterObject != null)
                {
                    UnitCharacter.FillFrom(_hero.CharacterObject, -1);
                }
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] Card FillFrom error: " + ex.Message, Colors.Red));
            }

            Skills = new MBBindingList<EncyclopediaSkillVM>();

            if (_hero == null) return;

            try
            {
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.OneHanded, _hero.GetSkillValue(DefaultSkills.OneHanded)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.TwoHanded, _hero.GetSkillValue(DefaultSkills.TwoHanded)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Polearm, _hero.GetSkillValue(DefaultSkills.Polearm)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Bow, _hero.GetSkillValue(DefaultSkills.Bow)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Crossbow, _hero.GetSkillValue(DefaultSkills.Crossbow)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Throwing, _hero.GetSkillValue(DefaultSkills.Throwing)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Athletics, _hero.GetSkillValue(DefaultSkills.Athletics)));
                Skills.Add(new EncyclopediaSkillVM(DefaultSkills.Riding, _hero.GetSkillValue(DefaultSkills.Riding)));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] Card skills error: " + ex.Message, Colors.Red));
            }
        }

        // Помощник для логов, чтобы не спотыкаться о null-имена
        private static string Cn(Hero h) => h?.Clan?.Name?.ToString() ?? "NULL";

        public void Click()
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Peasants] BEFORE: {_hero.Name}, age={_hero.Age:F1}, clan={Cn(_hero)}, " +
                    $"spouse={(_hero.Spouse == null ? "NULL" : _hero.Spouse.Name.ToString())}, " +
                    $"occ={_hero.Occupation}, female={_hero.IsFemale}, CanMarry={_hero.CanMarry()}",
                    Colors.Yellow));

                if (PeasantsBehavior.selectedHero != null)
                {
                    // ===================== БРАК =====================
                    _hero.ChangeState(CharacterStates.Active);
                    _hero.SetNewOccupation(Occupation.Lord);

                    if (PeasantsBehavior.selectedHero.Clan == null)
                    {
                        PeasantsBehavior.selectedHero.Clan = Hero.MainHero.Clan;
                    }

                    Clan targetClan = PeasantsBehavior.selectedHero.Clan ?? Hero.MainHero.Clan;
                    if (_hero.Clan != targetClan)
                    {
                        _hero.Clan = targetClan;
                    }

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Peasants] PRE-MARRIAGE CHECK: " +
                        $"groom={PeasantsBehavior.selectedHero.Name} " +
                        $"(occ={PeasantsBehavior.selectedHero.Occupation}, " +
                        $"clan={Cn(PeasantsBehavior.selectedHero)}, " +
                        $"female={PeasantsBehavior.selectedHero.IsFemale}, " +
                        $"CanMarry={PeasantsBehavior.selectedHero.CanMarry()}) " +
                        $"| bride={_hero.Name} " +
                        $"(occ={_hero.Occupation}, clan={Cn(_hero)}, " +
                        $"female={_hero.IsFemale}, CanMarry={_hero.CanMarry()})",
                        Colors.Yellow));

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Peasants] Trying marriage: {PeasantsBehavior.selectedHero.Name} + {_hero.Name}",
                        Colors.Yellow));

                    MarriageAction.Apply(PeasantsBehavior.selectedHero, _hero, true);

                    string mySpouse = _hero.Spouse == null ? "NULL" : _hero.Spouse.Name.ToString();
                    string groomSpouse = PeasantsBehavior.selectedHero.Spouse == null
                        ? "NULL" : PeasantsBehavior.selectedHero.Spouse.Name.ToString();

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Peasants] AFTER-MARRIAGE: mySpouse={mySpouse}, groomSpouse={groomSpouse}",
                        Colors.Yellow));

                    if (_hero.Spouse == null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] MarriageAction.Apply() ничего не сделал. " +
                            "У одного из участников CanMarry() == false — смотрите PRE-MARRIAGE CHECK выше.",
                            Colors.Red));
                    }
                    else
                    {
                        _hero.SetHasMet();
                        AddHeroToPartyAction.Apply(_hero, MobileParty.MainParty, true);
                    }
                }
                else
                {
                    // =================== НАЁМ КОМПАНЬОНА ===================
                    // Защитная проверка прямо перед наймом: если игрок каким-то
                    // образом дошёл сюда, имея лимит компаньонов уже заполненным —
                    // не нанимаем и корректно закрываем окно выбора.
                    if (PeasantsBehavior.IsCompanionLimitReached())
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] Companion limit reached — hire cancelled. " +
                            "(Защитная проверка в Click().)",
                            Colors.Red));

                        PeasantsBehavior.CleanupUnusedCandidates(null);
                        PeasantsBehavior.DeleteVMLayer();
                        return;
                    }

                    _hero.ChangeState(CharacterStates.Active);
                    _hero.SetNewOccupation(Occupation.Wanderer);

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Peasants] Hiring as companion: {_hero.Name}",
                        Colors.Yellow));

                    AddCompanionAction.Apply(Clan.PlayerClan, _hero);

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Peasants] AFTER-HIRE: occ={_hero.Occupation}, clan={Cn(_hero)}, " +
                        $"isCompanion={_hero.CompanionOf != null}, " +
                        $"totalCompanions={PeasantsBehavior.CountPlayerCompanions()}/" +
                        $"{Clan.PlayerClan.CompanionLimit}",
                        Colors.Yellow));

                    _hero.SetHasMet();
                    AddHeroToPartyAction.Apply(_hero, MobileParty.MainParty, true);
                }

                PeasantsBehavior.CleanupUnusedCandidates(_hero);
                PeasantsBehavior.DeleteVMLayer();
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] Click EXCEPTION: " + ex.Message + "\n" + ex.StackTrace,
                    Colors.Red));

                // Не оставляем окно открытым, если что-то упало посреди обработки
                try { PeasantsBehavior.DeleteVMLayer(); } catch { /* ignore */ }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
        }
    }
}