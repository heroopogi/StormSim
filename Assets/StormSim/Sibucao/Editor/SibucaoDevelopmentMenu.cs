using UnityEditor;

namespace StormSim.Sibucao.Editor
{
    public static class SibucaoDevelopmentMenu
    {
        const string MenuPath = "StormSim/Development/Show skip and clock controls";
        [MenuItem(MenuPath)]
        static void Toggle() => EditorPrefs.SetBool(SibucaoDevelopmentControls.EditorPreference, !SibucaoDevelopmentControls.Enabled);
        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, SibucaoDevelopmentControls.Enabled);
            return true;
        }
    }
}
