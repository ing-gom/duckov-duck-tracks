using Ducky.Sdk.Attributes;
using Ducky.Sdk.Localizations;

namespace DuckTracks;

/// <summary>
/// 로컬라이제이션 키 모음.
///
/// Ducky.Sdk가 이 클래스를 보고 <c>L</c> 클래스를 생성합니다. 코드에서는 항상
/// <c>L.Window.Title</c>처럼 <c>L</c>을 통해 읽고, 문자열 리터럴을 직접 쓰지 않습니다.
/// 키를 추가하면 assets/Locales의 ko/en/zh/zh-hant CSV 네 곳에 모두 넣어야 합니다.
///
/// 키에 모드 이름을 붙여 둡니다. SDK의 LocalizationService는 어셈블리 폴더의
/// Locales만 읽으므로 모드끼리 충돌할 일은 없지만, 로그나 저장 파일에서 어느 모드의
/// 키인지 바로 보이는 편이 낫습니다.
/// </summary>
[LanguageSupport("zh", "en", "ko", "zh-hant")]
public static class LK
{
    public static class Menu
    {
        /// <summary>일시정지 메뉴에 추가되는 버튼</summary>
        public const string OpenButton = "ducktracks_menu_open_button";
    }

    public static class Window
    {
        public const string Title = "ducktracks_window_title";
        public const string Close = "ducktracks_window_close";
        public const string Reset = "ducktracks_window_reset";
    }

    public static class Toggle
    {
        public const string MasterOn = "ducktracks_toggle_master_on";
        public const string MasterOff = "ducktracks_toggle_master_off";
    }

    /// <summary>모양 고르기</summary>
    public static class Shape
    {
        public const string Section = "ducktracks_shape_section";
        public const string SourceActual = "ducktracks_shape_source_actual";
        public const string SourceBuiltin = "ducktracks_shape_source_builtin";
        public const string SourceTexture = "ducktracks_shape_source_texture";
        public const string ActualHint = "ducktracks_shape_actual_hint";
        public const string Picker = "ducktracks_shape_picker";
        public const string Refresh = "ducktracks_shape_refresh";
        public const string NoTextures = "ducktracks_shape_no_textures";
        public const string FolderHint = "ducktracks_shape_folder_hint";
        public const string Preview = "ducktracks_shape_preview";
    }

    /// <summary>색</summary>
    public static class Colour
    {
        public const string Section = "ducktracks_colour_section";
        public const string Fresh = "ducktracks_colour_fresh";
        public const string Fade = "ducktracks_colour_fade";
        public const string Burst = "ducktracks_colour_burst";
        public const string Alpha = "ducktracks_colour_alpha";
        public const string BlendAlpha = "ducktracks_colour_blend_alpha";
        public const string BlendAdditive = "ducktracks_colour_blend_additive";
        public const string BlendHint = "ducktracks_colour_blend_hint";
        public const string Hex = "ducktracks_colour_hex";
        public const string GlowIntensity = "ducktracks_colour_glow_intensity";
    }

    /// <summary>크기·지속·간격</summary>
    public static class Shape2
    {
        public const string Section = "ducktracks_size_section";
        public const string AutoScale = "ducktracks_size_auto_scale";
        public const string Size = "ducktracks_size_size";
        public const string Life = "ducktracks_size_life";
        public const string Forever = "ducktracks_size_forever";
        public const string ForeverHint = "ducktracks_size_forever_hint";
    }

    /// <summary>걸음 알갱이</summary>
    public static class Burst
    {
        public const string Section = "ducktracks_burst_section";
        public const string Enable = "ducktracks_burst_enable";
        public const string Count = "ducktracks_burst_count";
        public const string Size = "ducktracks_burst_size";
        public const string Speed = "ducktracks_burst_speed";
        public const string Gravity = "ducktracks_burst_gravity";
        public const string Life = "ducktracks_burst_life";
        public const string ColourHint = "ducktracks_burst_colour_hint";
        public const string PickShape = "ducktracks_burst_pick_shape";
        public const string DefaultShape = "ducktracks_burst_default_shape";
        public const string ResetShape = "ducktracks_burst_reset_shape";
        public const string Drift = "ducktracks_burst_drift";
        public const string DriftHint = "ducktracks_burst_drift_hint";
        public const string DriftRate = "ducktracks_burst_drift_rate";
        public const string DriftScale = "ducktracks_burst_drift_scale";
        public const string DriftRise = "ducktracks_burst_drift_rise";
    }

    /// <summary>깜박임과 색 순환</summary>
    public static class Pulse
    {
        public const string Section = "ducktracks_pulse_section";
        public const string Enable = "ducktracks_pulse_enable";
        public const string Speed = "ducktracks_pulse_speed";
        public const string Depth = "ducktracks_pulse_depth";
        public const string CycleHue = "ducktracks_pulse_cycle_hue";
        public const string HueSpeed = "ducktracks_pulse_hue_speed";
        public const string GreyHint = "ducktracks_pulse_grey_hint";
    }

    /// <summary>모양 만들기</summary>
    public static class Editor
    {
        public const string Open = "ducktracks_editor_open";
        public const string Title = "ducktracks_editor_title";
        public const string Name = "ducktracks_editor_name";
        public const string Save = "ducktracks_editor_save";
        public const string Clear = "ducktracks_editor_clear";
        public const string Invert = "ducktracks_editor_invert";
        public const string Random = "ducktracks_editor_random";
        public const string Delete = "ducktracks_editor_delete";
    }
}
