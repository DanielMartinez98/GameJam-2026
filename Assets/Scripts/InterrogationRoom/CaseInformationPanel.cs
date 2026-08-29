using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //The police file: everything the department already knows, as opposed to everything the player works
    //out. It reads as a stack of pages turned one at a time - a page per suspect with their details and
    //the alibi they gave, then the autopsy report and whatever else the case came with.
    //
    //The suspect pages are not authored separately: they are the same profiles the phone dials and the
    //notebook names, so a suspect can never appear in the file under one name and be called under
    //another.
    public class CaseInformationPanel : RoomPanel
    {
        [SerializeField] private float portraitHeight = 220f;
        [SerializeField] private float navHeight = 54f;

        private int pageIndex;

        protected override string Title
        {
            get { return "Case file - " + PageTitle(pageIndex) + "   (" + (pageIndex + 1) + "/" + PageCount + ")"; }
        }

        //the suspects first, in the order they are authored, then the loose pages behind them
        private int PageCount
        {
            get
            {
                int suspects = director != null && director.Suspects != null ? director.Suspects.Length : 0;
                int extras = director != null && director.CasePages != null ? director.CasePages.Length : 0;
                return Mathf.Max(1, suspects + extras);
            }
        }

        protected override void OnOpened()
        {
            //the file is picked up at the front each time rather than wherever it was last left open
            pageIndex = 0;
        }

        protected override void Populate()
        {
            BuildNavigation();
            SuspectProfile suspect = SuspectAt(pageIndex);
            if (suspect != null)
            {
                BuildSuspectPage(suspect);
                return;
            }
            CasePage page = PageAt(pageIndex);
            if (page != null)
            {
                BuildCasePage(page);
                return;
            }
            AddText("Nothing has been filed on this case yet.", 22f, PanelUI.DimTextColor);
        }

        private void BuildSuspectPage(SuspectProfile suspect)
        {
            AddPortrait(suspect.portrait);
            AddText(suspect.displayName, 30f, PanelUI.HighlightColor);
            AddText(string.IsNullOrEmpty(suspect.information) ? "No details on file." : suspect.information,
                22f, PanelUI.TextColor);
            AddText("\nAlibi given", 24f, PanelUI.HighlightColor);
            AddText(string.IsNullOrEmpty(suspect.alibi) ? "No alibi on record." : suspect.alibi,
                22f, PanelUI.TextColor);
        }

        private void BuildCasePage(CasePage page)
        {
            AddPortrait(page.image);
            AddText(page.title, 30f, PanelUI.HighlightColor);
            AddText(page.body, 22f, PanelUI.TextColor);
        }

        //A page with no art skips the frame entirely rather than leaving a hole where one would be, so
        //the autopsy report reads as a page of text instead of a page of text with a gap over it.
        private void AddPortrait(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            GameObject frame = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            frame.layer = column.gameObject.layer;
            ((RectTransform)frame.transform).SetParent(column, false);
            Image image = frame.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            LayoutElement element = frame.GetComponent<LayoutElement>();
            element.minHeight = portraitHeight;
            element.preferredHeight = portraitHeight;
        }

        //the two arrows, side by side on one row so turning the page does not cost two rows of the page
        private void BuildNavigation()
        {
            GameObject row = new GameObject("Turn Page", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.layer = column.gameObject.layer;
            ((RectTransform)row.transform).SetParent(column, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = entrySpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            LayoutElement element = row.GetComponent<LayoutElement>();
            element.minHeight = navHeight;
            element.preferredHeight = navHeight;

            Button previous = PanelUI.CreateButton(row.transform, "Previous", "< Previous", 22f);
            previous.onClick.AddListener(delegate { TurnPage(-1); });
            previous.interactable = pageIndex > 0;
            CentreLabel(previous);

            Button next = PanelUI.CreateButton(row.transform, "Next", "Next >", 22f);
            next.onClick.AddListener(delegate { TurnPage(1); });
            next.interactable = pageIndex < PageCount - 1;
            CentreLabel(next);
        }

        private static void CentreLabel(Button button)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        //the file has a front and a back, so the arrows stop rather than wrapping round
        private void TurnPage(int step)
        {
            pageIndex = Mathf.Clamp(pageIndex + step, 0, PageCount - 1);
            Refresh();
        }

        private SuspectProfile SuspectAt(int index)
        {
            SuspectProfile[] suspects = director != null ? director.Suspects : null;
            if (suspects == null || index < 0 || index >= suspects.Length)
            {
                return null;
            }
            return suspects[index];
        }

        private CasePage PageAt(int index)
        {
            CasePage[] pages = director != null ? director.CasePages : null;
            int suspects = director != null && director.Suspects != null ? director.Suspects.Length : 0;
            int pageOffset = index - suspects;
            if (pages == null || pageOffset < 0 || pageOffset >= pages.Length)
            {
                return null;
            }
            return pages[pageOffset];
        }

        private string PageTitle(int index)
        {
            SuspectProfile suspect = SuspectAt(index);
            if (suspect != null)
            {
                return suspect.displayName;
            }
            CasePage page = PageAt(index);
            return page != null ? page.title : "empty";
        }
    }
}
