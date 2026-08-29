using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //A screen that opens over the interrogation room. All four of them - notebook, phone, case file and
    //memory picker - open and close the same way and are built the same way, so that part lives here and
    //each screen only has to say what goes in its column.
    //
    //The card itself can be handed in from the scene, in which case its contents are still filled in
    //here and only the look is the scene's. Left empty, the whole card is built, so the room can be
    //played before any of it has been drawn.
    public abstract class RoomPanel : MonoBehaviour
    {
        [SerializeField] protected GameObject panelRoot;
        //how much of the canvas the card covers when it is built rather than authored
        [SerializeField] protected Vector2 panelSize = new Vector2(0.72f, 0.82f);
        [SerializeField] protected float headerHeight = 76f;
        [SerializeField] protected float entrySpacing = 10f;
        [SerializeField] protected float entryHeight = 62f;

        protected InterrogationRoomDirector director;
        //where the rows go: the scrolling content, not the frame around it
        protected RectTransform column;
        protected ScrollRect scroll;
        protected TextMeshProUGUI header;

        //what goes across the top of the card
        protected abstract string Title { get; }

        //fills the column with whatever this screen is showing right now. Called every time something
        //changes, on a column that has just been emptied.
        protected abstract void Populate();

        public bool IsOpen
        {
            get { return panelRoot != null && panelRoot.activeSelf; }
        }

        public void Open(InterrogationRoomDirector owner)
        {
            director = owner;
            Build();
            if (panelRoot == null)
            {
                return;
            }
            panelRoot.SetActive(true);
            OnOpened();
            Refresh();
        }

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            OnClosed();
        }

        //a screen that keeps a selection between openings would show the player last call's clue still
        //picked, so each one gets to reset itself here
        protected virtual void OnOpened() { }

        protected virtual void OnClosed() { }

        //Rebuilds the column from scratch. Every screen here is a short list that changes when something
        //is pressed, so throwing the rows away and laying them out again is both simpler and cheaper
        //than keeping them in step with the state behind them.
        public void Refresh()
        {
            if (column == null)
            {
                return;
            }
            PanelUI.ClearChildren(column);
            if (header != null)
            {
                header.text = Title;
            }
            Populate();
            //Whatever is being shown now is a different thing from what was there a moment ago - the
            //next page of the file, the answer to a call - so it is shown from its top rather than at
            //however far down the last thing had been scrolled.
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void Build()
        {
            if (column != null)
            {
                return;
            }
            if (panelRoot == null)
            {
                Transform canvas = director != null ? director.PanelParent : null;
                if (canvas == null)
                {
                    Debug.LogWarning(GetType().Name + " has no panel to build into: give the director a Panel Parent or wire a Panel Root.");
                    return;
                }
                //named after the screen rather than after whatever object it was put on, because all
                //four of them usually sit on the director together and would otherwise share a name
                panelRoot = PanelUI.CreatePanel(canvas, GetType().Name, panelSize);
                //built hidden, because Open is what puts it up and Build runs from inside Open
                panelRoot.SetActive(false);
            }
            //an authored card can title itself by putting a "Title" text at its top level; any other
            //text on it is its own business and is left alone
            Transform authoredTitle = panelRoot.transform.Find("Title");
            header = authoredTitle != null ? authoredTitle.GetComponent<TextMeshProUGUI>() : null;
            if (header == null)
            {
                header = PanelUI.CreateHeader(panelRoot.transform, Title, headerHeight);
            }
            Button close = PanelUI.CreateCloseButton(panelRoot.transform, "X");
            close.onClick.AddListener(OnCloseButton);
            column = PanelUI.CreateColumn(panelRoot.transform, "Entries", headerHeight, entrySpacing);
            scroll = column.GetComponentInParent<ScrollRect>();
        }

        //the card's own X. Closing goes back through the director so the room knows nothing is open any
        //more and can put the table back in reach.
        private void OnCloseButton()
        {
            if (director != null)
            {
                director.ClosePanels();
            }
            else
            {
                Close();
            }
        }

        //a row in this screen's column, sized by the column and hooked up to what pressing it does
        protected Button AddEntry(string label, UnityEngine.Events.UnityAction onClick, bool interactable = true)
        {
            Button button = PanelUI.CreateButton(column, label, label, 24f);
            PanelUI.SetEntryHeight(button, entryHeight);
            button.interactable = interactable;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            return button;
        }

        //a line of text in the column that is not pressable: an instruction, an answer, an empty state
        protected TextMeshProUGUI AddText(string text, float fontSize, Color color)
        {
            TextMeshProUGUI label = PanelUI.CreateLabel(column, "Text", text, fontSize, TextAlignmentOptions.TopLeft);
            label.color = color;
            //deliberately no LayoutElement: the text component works out its own preferred height once
            //the column has given it a width, and a height pinned here would be measured before that
            //and cut a long answer off mid sentence.
            return label;
        }
    }
}
