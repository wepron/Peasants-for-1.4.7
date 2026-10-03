using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace Peasants
{
    // =========================================================================
    // PeasantsSettings — MCM-настройки мода Peasants.
    //
    // Две группы:
    //   1. Companions — возрастной диапазон кандидатов в компаньоны.
    //   2. Brides     — возрастной диапазон кандидатов в брак.
    //
    // Возраст кандидата теперь НЕ берётся из рандома, который раньше
    // вычислялся через new Random().Next(18, 25). Он ролится отдельно
    // для каждого кандидата в пределах [Min..Max] из этих настроек.
    // См. PeasantsBehavior.PickCandidateAge().
    // =========================================================================
    public class PeasantsSettings : AttributeGlobalSettings<PeasantsSettings>
    {
        public override string Id => "PeasantsSettings";
        public override string DisplayName => "Peasants for 1.4.7";
        public override string FormatType => "json";

        // ---------------------------------------------------------------------
        // Группа 1: Companions
        // ---------------------------------------------------------------------

        private int _companionMinAge = 22;

        [SettingPropertyGroup("Companions", GroupOrder = 0)]
        [SettingPropertyInteger("Minimum Companion Age", 18, 100, Order = 0,
            RequireRestart = false,
            HintText = "Minimum age for hireable peasant companions. Default 22.")]
        public int CompanionMinAge
        {
            get
            {
                try
                {
                    if (_companionMinAge < 18) return 18;
                    if (_companionMinAge > 100) return 100;
                    return _companionMinAge;
                }
                catch { return 22; }
            }
            set
            {
                try
                {
                    int v = value;
                    if (v < 18) v = 18;
                    if (v > 100) v = 100;
                    if (_companionMinAge != v)
                    {
                        _companionMinAge = v;
                        OnPropertyChanged(nameof(CompanionMinAge));
                    }
                }
                catch { }
            }
        }

        private int _companionMaxAge = 60;

        [SettingPropertyGroup("Companions", GroupOrder = 0)]
        [SettingPropertyInteger("Maximum Companion Age", 18, 100, Order = 1,
            RequireRestart = false,
            HintText = "Maximum age for hireable peasant companions. Default 60.")]
        public int CompanionMaxAge
        {
            get
            {
                try
                {
                    if (_companionMaxAge < 18) return 18;
                    if (_companionMaxAge > 100) return 100;
                    return _companionMaxAge;
                }
                catch { return 60; }
            }
            set
            {
                try
                {
                    int v = value;
                    if (v < 18) v = 18;
                    if (v > 100) v = 100;
                    if (_companionMaxAge != v)
                    {
                        _companionMaxAge = v;
                        OnPropertyChanged(nameof(CompanionMaxAge));
                    }
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------------
        // Группа 2: Brides
        // ---------------------------------------------------------------------
        // "Bride" здесь — это не строго пол, а любой кандидат на брак
        // (в текущей логике Part2 пол кандидата противоположен полу того
        //  члена клана, которого мы женим).

        private int _brideMinAge = 18;

        [SettingPropertyGroup("Brides", GroupOrder = 1)]
        [SettingPropertyInteger("Minimum Bride Age", 18, 100, Order = 0,
            RequireRestart = false,
            HintText = "Minimum age for marriage candidates. Default 18.")]
        public int BrideMinAge
        {
            get
            {
                try
                {
                    if (_brideMinAge < 18) return 18;
                    if (_brideMinAge > 100) return 100;
                    return _brideMinAge;
                }
                catch { return 18; }
            }
            set
            {
                try
                {
                    int v = value;
                    if (v < 18) v = 18;
                    if (v > 100) v = 100;
                    if (_brideMinAge != v)
                    {
                        _brideMinAge = v;
                        OnPropertyChanged(nameof(BrideMinAge));
                    }
                }
                catch { }
            }
        }

        private int _brideMaxAge = 22;

        [SettingPropertyGroup("Brides", GroupOrder = 1)]
        [SettingPropertyInteger("Maximum Bride Age", 18, 100, Order = 1,
            RequireRestart = false,
            HintText = "Maximum age for marriage candidates. Default 22.")]
        public int BrideMaxAge
        {
            get
            {
                try
                {
                    if (_brideMaxAge < 18) return 18;
                    if (_brideMaxAge > 100) return 100;
                    return _brideMaxAge;
                }
                catch { return 22; }
            }
            set
            {
                try
                {
                    int v = value;
                    if (v < 18) v = 18;
                    if (v > 100) v = 100;
                    if (_brideMaxAge != v)
                    {
                        _brideMaxAge = v;
                        OnPropertyChanged(nameof(BrideMaxAge));
                    }
                }
                catch { }
            }
        }
    }
}