using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.CampaignSystem.Hero;

// Псевдоним, чтобы убрать неоднозначность между TaleWorlds.Core.Extensions
// и TaleWorlds.Engine.GauntletUI.Extensions
using CoreExtensions = TaleWorlds.Core.Extensions;

namespace Peasants
{
    // =========================================================================
    // PeasantsBehavior — основной CampaignBehavior мода Peasants.
    // Адаптирован под Mount & Blade II: Bannerlord 1.4.7.
    // =========================================================================
    internal class PeasantsBehavior : CampaignBehaviorBase
    {
        // ---------------------------------------------------------------------
        // Статическое состояние UI-слоя
        // ---------------------------------------------------------------------
        public static Hero selectedHero;
        private static GauntletLayer layer;
        private static GauntletMovieIdentifier gauntletMovie;
        private static CharacterSelectorVM characterSelectorVM;

        // ---------------------------------------------------------------------
        // Список кандидатов, показанных в текущем окне выбора.
        // Нужен, чтобы после выбора удалить неиспользованных героев —
        // иначе они останутся в поселении как лишние NPC (Townsfolk) и могут
        // вызывать те же проблемы при разговоре с ними.
        // ---------------------------------------------------------------------
        public static List<Hero> CurrentCandidates = new List<Hero>();

        // ---------------------------------------------------------------------
        // UI: создание и удаление Gauntlet-слоя
        // ---------------------------------------------------------------------
        public static void CreateVMLayer(List<Hero> heroes)
        {
            try
            {
                if (layer != null) return;

                // 1.4.7: порядок аргументов — (string name, int order, bool ...)
                layer = new GauntletLayer("GauntletLayer", 1000, false);

                if (characterSelectorVM == null)
                {
                    characterSelectorVM = new CharacterSelectorVM(heroes);
                }

                characterSelectorVM.RefreshValues();

                // 1.4.7: LoadMovie возвращает GauntletMovieIdentifier
                gauntletMovie = layer.LoadMovie("CharacterSelector", characterSelectorVM);

                layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                ScreenManager.TopScreen.AddLayer(layer);
                layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(layer);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] CreateVMLayer error: " + ex.Message, Colors.Red));
            }
        }

        public static void DeleteVMLayer()
        {
            try
            {
                ScreenBase topScreen = ScreenManager.TopScreen;

                if (layer != null)
                {
                    layer.InputRestrictions.ResetInputRestrictions();
                    layer.IsFocusLayer = false;

                    if (gauntletMovie != null)
                    {
                        layer.ReleaseMovie(gauntletMovie);
                    }

                    topScreen.RemoveLayer(layer);
                }

                layer = null;
                gauntletMovie = null;
                characterSelectorVM = null;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] DeleteVMLayer error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // Удаление неиспользованных кандидатов.
        // Вызывается из CharacterCard.Click() после того, как выбран один герой.
        // ---------------------------------------------------------------------
        public static void CleanupUnusedCandidates(Hero chosen)
        {
            try
            {
                if (CurrentCandidates == null) return;

                foreach (Hero c in CurrentCandidates)
                {
                    if (c == null || c == chosen) continue;
                    if (!c.IsAlive) continue;

                    try
                    {
                        // ApplyByRemove убирает героя из мира без "настоящей" смерти
                        // и без уведомления игроку.
                        KillCharacterAction.ApplyByRemove(c, false);
                    }
                    catch
                    {
                        // Если API отличается — молча игнорируем,
                        // чтобы не сорвать основной сценарий.
                    }
                }

                CurrentCandidates.Clear();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] CleanupUnusedCandidates error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // CampaignBehaviorBase
        // ---------------------------------------------------------------------
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenuItems);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, TickDaily);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        // ---------------------------------------------------------------------
        // Ежедневный тик: чистим "зависшие" состояния у мёртвых/отключённых.
        // Наших текущих кандидатов пропускаем — иначе игра может
        // превратить их в Headman раньше, чем игрок успеет выбрать.
        // ---------------------------------------------------------------------
        private void TickDaily()
        {
            try
            {
                foreach (Hero hero in Campaign.Current.DeadOrDisabledHeroes)
                {
                    // защита: не трогаем кандидатов, которых мы только что создали
                    if (CurrentCandidates != null && CurrentCandidates.Contains(hero))
                        continue;

                    bool isTownsfolk = hero.IsAlive && hero.Occupation == Occupation.Townsfolk;
                    bool isVillager = hero.Occupation == Occupation.Villager;

                    if (isTownsfolk || isVillager)
                    {
                        hero.ChangeState(CharacterStates.Active);
                        hero.SetNewOccupation(Occupation.Headman);
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] TickDaily error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // Пункты меню в деревне
        // ---------------------------------------------------------------------
        private void AddMenuItems(CampaignGameStarter campaignGameStarter)
        {
            // --- "Arrange a marriage" ---
            campaignGameStarter.AddGameMenuOption(
                "village",
                "marry_peasant",
                "Arrange a marriage",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.IsEnabled = true;
                    args.Tooltip = new TextObject("Marry a family member to a local peasant", null);
                    return true;
                },
                args => { ShowFamilyList(); },
                false, 1, false, null);

            // --- "Recruit a companion" ---
            campaignGameStarter.AddGameMenuOption(
                "village",
                "recruit_peasant",
                "Recruit a companion",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.IsEnabled = true;
                    args.Tooltip = new TextObject("Hire a peasant Companion", null);
                    return Clan.PlayerClan.CompanionLimit > Clan.PlayerClan.Companions.Count;
                },
                args => { HirePeasant(); },
                false, 1, false, null);
        }

        // ---------------------------------------------------------------------
        // Шаг 1: выбор члена клана, которого хотим женить
        // ---------------------------------------------------------------------
        private void ShowFamilyList()
        {
            try
            {
                List<InquiryElement> inquiryElements = new List<InquiryElement>();

                foreach (Hero hero in Campaign.Current.AliveHeroes)
                {
                    bool suitable =
                        hero.Clan == Hero.MainHero.Clan &&
                        hero.Age >= 18f &&
                        hero.Spouse == null &&
                        hero.Occupation == Occupation.Lord;

                    if (!suitable) continue;

                    inquiryElements.Add(new InquiryElement(
                        hero.CharacterObject.HeroObject,
                        hero.Name + " - " + hero.Age.ToString("0"),
                        new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject))));
                }

                if (inquiryElements.Count < 1)
                {
                    InformationManager.ShowInquiry(
                        new InquiryData(
                            "Arrange Marriage Not Possible",
                            "You have no single clan members",
                            true, false,
                            "OK", "",
                            null, null),
                        true, false);
                    return;
                }

                // 1.4.7: MultiSelectionInquiryData имеет новую сигнатуру
                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        "Members Suitable For Marriage",   // titleText
                        "",                                 // descriptionText
                        inquiryElements,                    // List<InquiryElement>
                        true,                               // isExitShown
                        1,                                  // minSelectableOptionCount
                        1,                                  // maxSelectableOptionCount
                        "Continue",                         // affirmativeText
                        null,                               // negativeText
                        args =>                             // affirmativeAction
                        {
                            if (args == null || !args.Any()) return;

                            InformationManager.HideInquiry();

                            SubModule.ExecuteActionOnNextTick(() =>
                            {
                                Hero picked = args.Select(e => e.Identifier as Hero).FirstOrDefault();
                                if (picked != null) Part2(picked);
                            });
                        },
                        null,                               // negativeAction
                        "",                                 // soundEventPath
                        false                               // isSearchAvailable
                    ),
                    false,
                    false);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] ShowFamilyList error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // Шаг 2: генерируем 10 кандидатов и открываем VM-слой
        // ---------------------------------------------------------------------
        private void Part2(Hero hero)
        {
            try
            {
                selectedHero = hero;

                List<Hero> candidates = new List<Hero>();
                List<CharacterObject> troops = new List<CharacterObject>();

                foreach (Hero notable in Settlement.CurrentSettlement.Notables)
                {
                    foreach (CharacterObject troop in notable.VolunteerTypes)
                    {
                        if (troop != null && hero.IsFemale != troop.IsFemale)
                        {
                            troops.Add(troop);
                        }
                    }
                }

                for (int i = 0; i < 10; i++)
                {
                    bool tryRecruit = MBRandom.RandomInt(0, 100) > 75 && troops.Count > 0;

                    if (tryRecruit)
                    {
                        CharacterObject troop = CoreExtensions.GetRandomElement<CharacterObject>(troops);
                        // кандидат в супруги — НЕ компаньон, occupation станет Lord после свадьбы
                        candidates.Add(CreateRecruit(troop, asCompanion: false));
                    }
                    else
                    {
                        candidates.Add(CreatePeasant(!hero.IsFemale, asCompanion: false));
                    }
                }

                CurrentCandidates = candidates;
                CreateVMLayer(candidates);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] Part2 error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // Создание "чистого" крестьянина (низкоуровневый герой)
        // ---------------------------------------------------------------------
        private Hero CreatePeasant(bool isFemale, bool asCompanion)
        {
            Hero newHero;

            CharacterObject template = isFemale
                ? Settlement.CurrentSettlement.Culture.Townswoman
                : Settlement.CurrentSettlement.Culture.Townsman;

            newHero = HeroCreator.CreateSpecialHero(
                template,
                Settlement.CurrentSettlement,
                null,
                null,                            // ← без клана: MarriageAction/AddCompanionAction сами добавят
                new Random().Next(18, 25));      // ← возраст (5-й параметр, подтверждено декомпиляцией)

            ApplySkillsFromTemplate(newHero, template);

            List<Equipment> equipments = template.BattleEquipments.ToList();
            if (equipments.Count > 0)
            {
                newHero.CharacterObject.Equipment.FillFrom(
                    CoreExtensions.GetRandomElement<Equipment>(equipments), true);
            }

            // 1.4.7: 6 = Disabled -> нужно Active (1)
            newHero.ChangeState(CharacterStates.Active);

            // Если создаём именно компаньона — сразу ставим Wanderer,
            // чтобы не оставлять героя с Occupation.Townsfolk.
            // Для кандидатов в супруги оставляем Townsfolk (после свадьбы станет Lord).
            if (asCompanion)
            {
                newHero.SetNewOccupation(Occupation.Wanderer);
            }

            return newHero;
        }

        // ---------------------------------------------------------------------
        // Создание героя по шаблону (например, из добровольцев)
        // ---------------------------------------------------------------------
        private Hero CreateRecruit(CharacterObject template, bool asCompanion)
        {
            Hero newHero = HeroCreator.CreateSpecialHero(
                template,
                Settlement.CurrentSettlement,
                null,
                null,                                // ← без клана
                new Random().Next(18, 25));          // ← возраст

            ApplySkillsFromTemplate(newHero, template);

            List<Equipment> equipments = template.BattleEquipments.ToList();
            if (equipments.Count > 0)
            {
                newHero.CharacterObject.Equipment.FillFrom(
                    CoreExtensions.GetRandomElement<Equipment>(equipments), true);
            }

            newHero.ChangeState(CharacterStates.Active);

            if (asCompanion)
            {
                newHero.SetNewOccupation(Occupation.Wanderer);
            }

            return newHero;
        }

        // ---------------------------------------------------------------------
        // Общий помощник: производные скиллы из шаблона.
        //
        // В Bannerlord 1.4.7 метод GetSkillsDerivedFromTraits был удалён
        // из DefaultCharacterDevelopmentModel, поэтому здесь ничего не делаем.
        // ---------------------------------------------------------------------
        private static void ApplySkillsFromTemplate(Hero hero, CharacterObject template)
        {
            try
            {
                if (hero == null || template == null) return;
                // В 1.4.7 метод GetSkillsDerivedFromTraits отсутствует.
            }
            catch
            {
                // ignore
            }
        }

        // ---------------------------------------------------------------------
        // Найм компаньона
        // ---------------------------------------------------------------------
        private void HirePeasant()
        {
            try
            {
                selectedHero = null;

                List<Hero> candidates = new List<Hero>();
                List<CharacterObject> troops = new List<CharacterObject>();

                foreach (Hero notable in Settlement.CurrentSettlement.Notables)
                {
                    foreach (CharacterObject troop in notable.VolunteerTypes)
                    {
                        if (troop != null)
                        {
                            troops.Add(troop);
                        }
                    }
                }

                for (int i = 0; i < 10; i++)
                {
                    bool tryRecruit = MBRandom.RandomInt(0, 100) > 75 && troops.Count > 0;

                    if (tryRecruit)
                    {
                        CharacterObject troop = CoreExtensions.GetRandomElement<CharacterObject>(troops);
                        candidates.Add(CreateRecruit(troop, asCompanion: true));
                    }
                    else
                    {
                        candidates.Add(CreatePeasant(MBRandom.RandomInt(0, 100) > 50, asCompanion: true));
                    }
                }

                CurrentCandidates = candidates;
                CreateVMLayer(candidates);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] HirePeasant error: " + ex.Message, Colors.Red));
            }
        }
    }
}