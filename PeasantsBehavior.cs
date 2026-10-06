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
        // Нужен, чтобы после выбора удалить неиспользованных героев.
        // ---------------------------------------------------------------------
        public static List<Hero> CurrentCandidates = new List<Hero>();

        // ---------------------------------------------------------------------
        // Лимит компаньонов.
        //
        // ВАЖНО: НЕ используем Clan.PlayerClan.Companions.Count — это
        // кэшированный список, который на старте кампании (до открытия
        // вкладки клана) остаётся пустым и позволяет нанимать бесконечно.
        //
        // Считаем напрямую по всем живым героям мира, у которых
        // CompanionOf == PlayerClan. Это единственный источник, который
        // обновляется сразу после AddCompanionAction.Apply.
        // ---------------------------------------------------------------------
        public static int CountPlayerCompanions()
        {
            try
            {
                Clan player = Clan.PlayerClan;
                if (player == null) return 0;

                int count = 0;

                if (Campaign.Current != null && Campaign.Current.AliveHeroes != null)
                {
                    foreach (Hero h in Campaign.Current.AliveHeroes)
                    {
                        try
                        {
                            if (h == null) continue;
                            if (!h.IsAlive) continue;
                            if (h.CompanionOf == player) count++;
                        }
                        catch
                        {
                            // Пропускаем кривого героя, не валим весь подсчёт.
                        }
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] CountPlayerCompanions error: " + ex.Message, Colors.Red));
                return 0;
            }
        }

        public static bool IsCompanionLimitReached()
        {
            try
            {
                Clan player = Clan.PlayerClan;
                if (player == null) return false;

                int limit = player.CompanionLimit;
                int current = CountPlayerCompanions();

                return current >= limit;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] IsCompanionLimitReached error: " + ex.Message, Colors.Red));
                return false;
            }
        }

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

                // Не оставляем слой висеть в полусобранном состоянии
                try { DeleteVMLayer(); } catch { /* ignore */ }
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

                    if (topScreen != null)
                    {
                        topScreen.RemoveLayer(layer);
                    }
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
        // chosen == null — "закрытие без выбора", удаляем всех.
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
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] Cleanup failed for " +
                            (c.Name?.ToString() ?? "?") + ": " + ex.Message,
                            Colors.Red));
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
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        // ---------------------------------------------------------------------
        // Пункты меню в деревне
        // ---------------------------------------------------------------------
        private void AddMenuItems(CampaignGameStarter campaignGameStarter)
        {
            try
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
                // Видимость через IsCompanionLimitReached() — не через
                // Clan.PlayerClan.Companions, у которого кэш протухает.
                campaignGameStarter.AddGameMenuOption(
                    "village",
                    "recruit_peasant",
                    "Recruit a companion",
                    args =>
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        args.IsEnabled = true;
                        args.Tooltip = new TextObject("Hire a peasant Companion", null);
                        return !IsCompanionLimitReached();
                    },
                    args => { HirePeasant(); },
                    false, 1, false, null);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] AddMenuItems error: " + ex.Message, Colors.Red));
            }
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
                    try
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
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] Skipped hero in family list: " + ex.Message, Colors.Red));
                    }
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

                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        "Members Suitable For Marriage",
                        "",
                        inquiryElements,
                        true,
                        1,
                        1,
                        "Continue",
                        null,
                        args =>
                        {
                            try
                            {
                                if (args == null || !args.Any()) return;

                                InformationManager.HideInquiry();

                                SubModule.ExecuteActionOnNextTick(() =>
                                {
                                    Hero picked = args.Select(e => e.Identifier as Hero).FirstOrDefault();
                                    if (picked != null) Part2(picked);
                                });
                            }
                            catch (Exception ex)
                            {
                                InformationManager.DisplayMessage(new InformationMessage(
                                    "[Peasants] Inquiry affirmative error: " + ex.Message, Colors.Red));
                            }
                        },
                        null,
                        "",
                        false
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
                if (Settlement.CurrentSettlement == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Not in a settlement — cannot arrange marriage.",
                        Colors.Red));
                    return;
                }

                selectedHero = hero;

                List<Hero> candidates = new List<Hero>();
                List<CharacterObject> troops = new List<CharacterObject>();

                try
                {
                    foreach (Hero notable in Settlement.CurrentSettlement.Notables)
                    {
                        if (notable == null) continue;

                        foreach (CharacterObject troop in notable.VolunteerTypes)
                        {
                            if (troop != null && hero.IsFemale != troop.IsFemale)
                            {
                                troops.Add(troop);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Gathering troops error: " + ex.Message, Colors.Red));
                }

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        // Возраст ролится ОТДЕЛЬНО для каждого кандидата.
                        int age = PickCandidateAge(asCompanion: false);

                        bool tryRecruit = MBRandom.RandomInt(0, 100) > 75 && troops.Count > 0;

                        if (tryRecruit)
                        {
                            CharacterObject troop = CoreExtensions.GetRandomElement<CharacterObject>(troops);
                            candidates.Add(CreateRecruit(troop, asCompanion: false, age: age));
                        }
                        else
                        {
                            candidates.Add(CreatePeasant(!hero.IsFemale, asCompanion: false, age: age));
                        }
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] Candidate creation error: " + ex.Message, Colors.Red));
                    }
                }

                if (candidates.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] No candidates could be created.", Colors.Red));
                    return;
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
        // Возраст одного кандидата.
        //
        // asCompanion == true  → берём диапазон из группы Companions.
        // asCompanion == false → берём диапазон из группы Brides.
        //
        // Если Min > Max (игрок случайно так выставил) — Max подтягивается
        // к Min, чтобы MBRandom.RandomInt() не получал пустой интервал.
        // ---------------------------------------------------------------------
        private static int PickCandidateAge(bool asCompanion)
        {
            int min, max;

            try
            {
                PeasantsSettings s = PeasantsSettings.Instance;

                if (asCompanion)
                {
                    min = s != null ? s.CompanionMinAge : 22;
                    max = s != null ? s.CompanionMaxAge : 60;
                }
                else
                {
                    min = s != null ? s.BrideMinAge : 18;
                    max = s != null ? s.BrideMaxAge : 22;
                }
            }
            catch
            {
                if (asCompanion) { min = 22; max = 60; }
                else { min = 18; max = 22; }
            }

            if (min < 18) min = 18;
            if (max > 100) max = 100;
            if (max < min) max = min;

            // MBRandom.RandomInt(min, max) — верхняя граница эксклюзивна,
            // поэтому +1, чтобы MaxAge мог реально выпасть.
            return MBRandom.RandomInt(min, max + 1);
        }

        // =====================================================================
        // ПРИНУДИТЕЛЬНАЯ УСТАНОВКА ВОЗРАСТА
        //
        // Используем ПУБЛИЧНЫЙ Hero.SetBirthDay(CampaignTime), а НЕ рефлексию.
        //
        // Почему так:
        //   Hero.SetBirthDay внутри делает:
        //       _birthDay = birthday;
        //       _defaultAge = birthday.IsNow ? 0.001f : _birthDay.ElapsedYearsUntilNow;
        //
        //   А свойство Age в 1.4.7 работает так:
        //       if (CampaignOptions.IsLifeDeathCycleDisabled) return _defaultAge;
        //       if (IsAlive) return _birthDay.ElapsedYearsUntilNow;
        //
        //   То есть, если игрок ВЫКЛЮЧИЛ старение (IsLifeDeathCycleDisabled == true),
        //   Age берётся из _defaultAge. Если бы мы писали в _birthDay через
        //   рефлексию, _defaultAge остался бы 65 (дефолт шаблона Townsman),
        //   и все кандидаты выглядели бы 65-летними.
        //
        //   SetBirthDay обновляет ОБА поля сразу — поэтому корректно работает
        //   при любом значении IsLifeDeathCycleDisabled.
        //
        // Формула:
        //   CampaignTime.YearsFromNow(-years) = точка ровно на `years` лет НАЗАД
        //   от текущего момента. НЕ путать с CampaignTime.Years(years), который
        //   даёт "years лет от старта кампании" — именно из-за этой путаницы
        //   возраст раньше уезжал на "+30 лет".
        // =====================================================================
        private static void ForceSetAge(Hero hero, int years)
        {
            if (hero == null) return;

            try
            {
                // Гарантируем, что герой активен: движок не перезаписывает
                // BirthDay / DefaultAge у активного героя.
                if (hero.HeroState != CharacterStates.Active)
                {
                    hero.ChangeState(CharacterStates.Active);
                }

                // Публичный метод. Обновляет и _birthDay, и _defaultAge.
                hero.SetBirthDay(CampaignTime.YearsFromNow(-years));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Peasants] ForceSetAge error: " + ex.Message, Colors.Red));
            }
        }

        // ---------------------------------------------------------------------
        // Создание "чистого" крестьянина (низкоуровневый герой)
        // ---------------------------------------------------------------------
        private Hero CreatePeasant(bool isFemale, bool asCompanion, int age)
        {
            CharacterObject template = isFemale
                ? Settlement.CurrentSettlement.Culture.Townswoman
                : Settlement.CurrentSettlement.Culture.Townsman;

            Hero newHero = HeroCreator.CreateSpecialHero(
                template,
                Settlement.CurrentSettlement,
                null,
                null,
                age);

            // Порядок важен: активируем и ставим BirthDay/DefaultAge.
            // ForceSetAge сам делает ChangeState(Active), если ещё не активен.
            ForceSetAge(newHero, age);

            // BattleEquipments может быть null у некоторых шаблонов
            List<Equipment> equipments = template.BattleEquipments?.ToList() ?? new List<Equipment>();
            if (equipments.Count > 0)
            {
                newHero.CharacterObject.Equipment.FillFrom(
                    CoreExtensions.GetRandomElement<Equipment>(equipments), true);
            }

            if (asCompanion)
            {
                newHero.SetNewOccupation(Occupation.Wanderer);
            }

            return newHero;
        }

        // ---------------------------------------------------------------------
        // Создание героя по шаблону (например, из добровольцев)
        // ---------------------------------------------------------------------
        private Hero CreateRecruit(CharacterObject template, bool asCompanion, int age)
        {
            Hero newHero = HeroCreator.CreateSpecialHero(
                template,
                Settlement.CurrentSettlement,
                null,
                null,
                age);

            // Порядок важен: активируем и ставим BirthDay/DefaultAge.
            ForceSetAge(newHero, age);

            List<Equipment> equipments = template.BattleEquipments?.ToList() ?? new List<Equipment>();
            if (equipments.Count > 0)
            {
                newHero.CharacterObject.Equipment.FillFrom(
                    CoreExtensions.GetRandomElement<Equipment>(equipments), true);
            }

            if (asCompanion)
            {
                newHero.SetNewOccupation(Occupation.Wanderer);
            }

            return newHero;
        }

        // ---------------------------------------------------------------------
        // Найм компаньона
        // ---------------------------------------------------------------------
        private void HirePeasant()
        {
            try
            {
                if (Settlement.CurrentSettlement == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Not in a settlement — cannot hire.",
                        Colors.Red));
                    return;
                }

                // Защитная проверка перед открытием окна выбора.
                // Меню деревни могло быть отрисовано до того, как игрок
                // нанял предыдущего компаньона, поэтому здесь проверяем ещё раз.
                if (IsCompanionLimitReached())
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Companion limit reached — cannot hire more.",
                        Colors.Red));
                    return;
                }

                selectedHero = null;

                List<Hero> candidates = new List<Hero>();
                List<CharacterObject> troops = new List<CharacterObject>();

                try
                {
                    foreach (Hero notable in Settlement.CurrentSettlement.Notables)
                    {
                        if (notable == null) continue;

                        foreach (CharacterObject troop in notable.VolunteerTypes)
                        {
                            if (troop != null)
                            {
                                troops.Add(troop);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] Gathering troops error: " + ex.Message, Colors.Red));
                }

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        // Возраст ролится ОТДЕЛЬНО для каждого кандидата.
                        int age = PickCandidateAge(asCompanion: true);

                        bool tryRecruit = MBRandom.RandomInt(0, 100) > 75 && troops.Count > 0;

                        if (tryRecruit)
                        {
                            CharacterObject troop = CoreExtensions.GetRandomElement<CharacterObject>(troops);
                            candidates.Add(CreateRecruit(troop, asCompanion: true, age: age));
                        }
                        else
                        {
                            candidates.Add(CreatePeasant(MBRandom.RandomInt(0, 100) > 50, asCompanion: true, age: age));
                        }
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Peasants] Candidate creation error: " + ex.Message, Colors.Red));
                    }
                }

                if (candidates.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[Peasants] No candidates could be created.", Colors.Red));
                    return;
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