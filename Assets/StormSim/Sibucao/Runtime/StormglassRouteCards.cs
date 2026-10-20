using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using StormSim.Sibucao;

namespace StormSim.UI
{
    public sealed partial class StormglassUI
    {
        RectTransform routeCards;
        TMP_Text routeCardsHeading, routeCardsHelp;
        readonly List<Button> routeButtons = new List<Button>();
        readonly List<Image> routeButtonImages = new List<Image>();
        readonly List<TMP_Text> routeButtonLabels = new List<TMP_Text>();
        bool routePointerHovered;

        bool RouteCardsVisible => routeCards && routeCards.gameObject.activeSelf;
        IReadOnlyList<Button> PointerButtons => RouteCardsVisible ? routeButtons : buttons;
        RectTransform PointerViewport => RouteCardsVisible ? null : viewport;

        void CreateRouteCards()
        {
            routeCards = Rect("Route choices", hud, new Vector2(980, 230));
            routeCards.anchorMin = routeCards.anchorMax = new Vector2(.5f, 0);
            routeCards.pivot = new Vector2(.5f, 0);
            routeCards.anchoredPosition = new Vector2(0, 22);
            Panel(routeCards, new Color(StormglassTheme.Background.r, StormglassTheme.Background.g, StormglassTheme.Background.b, .94f));

            routeCardsHeading = RouteText("Route heading", routeCards, new Vector2(920, 28), new Vector2(0, 94), 19, TextAlignmentOptions.Center);
            routeCardsHeading.fontStyle = FontStyles.Bold;
            routeCardsHeading.text = "CHOOSE GUIDANCE — INSPECT LEFT, AHEAD, AND RIGHT";

            for (int i = 0; i < 3; i++) CreateRouteCard(i);

            routeCardsHelp = RouteText("Route help", routeCards, new Vector2(920, 24), new Vector2(0, -98), 15, TextAlignmentOptions.Center);
            routeCardsHelp.color = StormglassTheme.Muted;
            routeCardsHelp.text = "1 / 2 / 3 selects guidance · Walk into a passage to commit";
            routeCards.gameObject.SetActive(false);
        }

        TMP_Text RouteText(string name, Transform parent, Vector2 size, Vector2 position, float fontSize, TextAlignmentOptions alignment)
        {
            var rect = Rect(name, parent, size);
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.bodyFont;
            text.fontSize = fontSize;
            text.color = StormglassTheme.Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        void CreateRouteCard(int index)
        {
            var rect = Rect("Route card " + index, routeCards, new Vector2(300, 142));
            rect.anchoredPosition = new Vector2((index - 1) * 314, -2);
            Panel(rect, StormglassTheme.Surface);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
            colors.pressedColor = new Color(.82f, .82f, .82f, 1);
            colors.fadeDuration = .08f;
            button.colors = colors;
            int selected = index;
            button.onClick.AddListener(() => SelectRouteCard(selected));
            rect.gameObject.AddComponent<StormglassFocus>().sprite = theme.PanelSprite;

            var label = RouteText("Route card label", rect, new Vector2(264, 112), Vector2.zero, 18, TextAlignmentOptions.MidlineLeft);
            label.enableAutoSizing = true;
            label.fontSizeMin = 13;
            label.fontSizeMax = 18;
            routeButtons.Add(button);
            routeButtonImages.Add(image);
            routeButtonLabels.Add(label);
        }

        void SelectRouteCard(int index)
        {
            var choices = S && S.missionGuide ? S.missionGuide.RouteChoices : null;
            if (choices && choices.Previewing) choices.SelectPreview(index);
        }

        void UpdateRouteCards()
        {
            var choices = S && S.missionGuide ? S.missionGuide.RouteChoices : null;
            bool show = screenKey == "HUD" && !Prep && choices && choices.Previewing;
            if (routeCards.gameObject.activeSelf != show) routeCards.gameObject.SetActive(show);
            routePointerHovered = false;
            if (!show) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) choices.SelectPreview(0);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) choices.SelectPreview(1);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) choices.SelectPreview(2);
            }
            var pad=Gamepad.current;
            if(pad!=null)
            {
                if(pad.dpad.left.wasPressedThisFrame)SelectRouteCard((choices.PreviewSelection+2)%3);
                if(pad.dpad.right.wasPressedThisFrame)SelectRouteCard((choices.PreviewSelection+1)%3);
            }

            float textScale=S.settings.textScale;
            routeCards.sizeDelta=new Vector2(980,230*textScale);
            routeCardsHeading.rectTransform.sizeDelta=new Vector2(920,32*textScale);
            routeCardsHelp.rectTransform.sizeDelta=new Vector2(920,28*textScale);
            routeCardsHeading.rectTransform.anchoredPosition=new Vector2(0,routeCards.sizeDelta.y*.5f-24*textScale);
            routeCardsHelp.rectTransform.anchoredPosition=new Vector2(0,-routeCards.sizeDelta.y*.5f+22*textScale);
            var section = choices.Current;
            if (!section) return;
            var missions = new[] { section.left, null, section.right };
            var directions = new[] { "1  LEFT", "2  AHEAD", "3  RIGHT" };
            for (int i = 0; i < 3; i++)
            {
                ((RectTransform)routeButtons[i].transform).sizeDelta=new Vector2(300,142*textScale);
                routeButtonLabels[i].rectTransform.sizeDelta=new Vector2(264,112*textScale);
                routeButtonLabels[i].fontSizeMin=18*S.settings.textScale;
                routeButtonLabels[i].fontSizeMax=20*S.settings.textScale;
                bool selected = choices.PreviewSelection == i;
                var target = choices.PreviewTarget(i);
                var mission = missions[i];
                bool closed = mission && mission.Closed;
                string title = mission ? mission.title : "Continue on the main route";
                string purpose = mission ? (mission.teamwork ? "ASSISTANCE" : "SIDE TASK") : "MAIN ROUTE";
                string condition = RouteCondition(target, closed);
                float distance = SibucaoRouteChoices.Flat(S.player.transform.position, target);
                string accent = ColorUtility.ToHtmlStringRGB(selected ? StormglassTheme.OnAccent : closed ? StormglassTheme.Warning : StormglassTheme.Accent);
                routeButtonLabels[i].text = "<color=#" + accent + "><b>" + directions[i] + "  ·  " + distance.ToString("0") + " m</b></color>\n" +
                    title + "\n<size=85%>" + condition + "  ·  " + purpose + "</size>";
                routeButtonImages[i].color = selected ? StormglassTheme.Accent : StormglassTheme.Surface;
                routeButtonLabels[i].color = selected ? StormglassTheme.OnAccent : StormglassTheme.Ink;
            }

            routeCardsHeading.fontSize = 19 * S.settings.textScale;
            routeCardsHelp.fontSize = 15 * S.settings.textScale;
            routeCardsHelp.text = XRSettings.isDeviceActive
                ? "Point and release trigger to guide · Enter a passage to commit"
                : "1 / 2 / 3 or gamepad D-pad selects guidance · Enter a passage to commit";
        }

        string RouteCondition(Vector3 target, bool closed)
        {
            if (closed) return "FLOODED / CLOSED";
            var flood = S.GetComponent<FloodSimulationManager>();
            float depth = flood ? flood.GetWaterDepth(target) : 0;
            if (depth >= .35f) return "DEEP WATER";
            if (depth >= .05f) return "SHALLOW WATER";
            return "DRY FOOTING";
        }
    }
}
