using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //Which of the three voices a line of a screen is written in. The look of each one lives in its own
    //prefab, so this only says which of them a line is, never how it reads.
    public enum PanelText
    {
        //what the screen is actually telling you
        Body,
        //an instruction, an empty state, something being read back
        Dim,
        //something that has just happened and is worth catching the eye
        Note
    }

    //A screen that opens over the interrogation room. All four of them - notebook, phone, case file and
    //memory picker - open and close the same way, so that part lives here and each one only has to say
    //what goes in its list.
    //
    //None of them is built. The card is drawn art in the scene, the title and the scrolling list are
    //objects on it, and every row is a prefab. What this does is switch the card on, put the right words
    //in the title, and fill the list with copies of the prefabs. Everything about how any of it looks is
    //settled in the editor and nothing here needs to know about it.
    public abstract class RoomPanel : MonoBehaviour
    {
        [Header("The card")]
        [SerializeField] protected GameObject panelRoot;
        [SerializeField] protected TextMeshProUGUI titleLabel;
        //the object the rows are dropped into: the scrolling content, not the frame around it
        [SerializeField] protected RectTransform itemsParent;
        //put back to the top whenever the list changes. Left empty, the list simply does not scroll.
        [SerializeField] protected ScrollRect scroll;

        [Header("Buttons on the card")]
        [SerializeField] protected Button closeButton;
        //taken off the card when there is nowhere to go back to, rather than sitting there doing nothing
        [SerializeField] protected Button backButton;
        [SerializeField] protected Button forwardButton;

        [Header("Row prefabs")]
        [SerializeField] protected PanelEntry entryPrefab;
        [SerializeField] protected TextMeshProUGUI bodyTextPrefab;
        [SerializeField] protected TextMeshProUGUI dimTextPrefab;
        [SerializeField] protected TextMeshProUGUI noteTextPrefab;

        protected InterrogationRoomDirector director;
        private bool hooked;
        //said once and then not again, because it would otherwise be said every time the list changes
        private bool complained;

        //what goes across the top of the card
        protected abstract string Title { get; }

        //fills the list with whatever this screen is showing right now. Called every time something
        //changes, on a list that has just been emptied.
        protected abstract void Populate();

        public bool IsOpen
        {
            get { return panelRoot != null && panelRoot.activeSelf; }
        }

        //Whether the card carries its own step-back arrow. A screen that has one leaves the "back" row
        //out of its list, so the same step is not offered twice on the same page.
        protected bool HasBackButton
        {
            get { return backButton != null; }
        }

        protected bool HasForwardButton
        {
            get { return forwardButton != null; }
        }

        //The card sits in the scene where it was drawn, which is to say switched on, so the first thing
        //that happens to it is being put away. Opening is what puts it back up.
        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void Open(InterrogationRoomDirector owner)
        {
            director = owner;
            if (panelRoot == null)
            {
                Missing("Panel Root");
                return;
            }
            HookButtons();
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

        //What the drawn arrows do, for the screens that have them.
        protected virtual bool CanGoBack { get { return false; } }

        protected virtual void GoBack() { }

        protected virtual bool CanGoForward { get { return false; } }

        protected virtual void GoForward() { }

        //Empties the list and fills it again. Every screen here is a short list that changes when
        //something is pressed, so throwing the rows away and laying them out again is both simpler and
        //cheaper than keeping them in step with the state behind them.
        public void Refresh()
        {
            if (itemsParent == null)
            {
                Missing("Items Parent");
                return;
            }
            PanelUI.ClearChildren(itemsParent);
            if (titleLabel != null)
            {
                titleLabel.text = Title;
            }
            Populate();
            //the arrows are part of the same page as the rows, so they are put right at the same time
            //rather than being left offering a step that is no longer there
            if (backButton != null)
            {
                backButton.gameObject.SetActive(CanGoBack);
            }
            if (forwardButton != null)
            {
                forwardButton.gameObject.SetActive(CanGoForward);
            }
            //Whatever is being shown now is a different thing from what was there a moment ago - the
            //next page of the file, the answer to a call - so it is shown from its top rather than at
            //however far down the last thing had been scrolled.
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        //hooked once, on the first opening, rather than every time the card goes up
        private void HookButtons()
        {
            if (hooked)
            {
                return;
            }
            hooked = true;
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButton);
            }
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackButton);
            }
            if (forwardButton != null)
            {
                forwardButton.onClick.AddListener(OnForwardButton);
            }
        }

        //the card's own exit. Closing goes back through the director so the room knows nothing is open
        //any more and can put the table back in reach.
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

        private void OnBackButton()
        {
            if (CanGoBack)
            {
                GoBack();
            }
        }

        private void OnForwardButton()
        {
            if (CanGoForward)
            {
                GoForward();
            }
        }

        //a pressable row in this screen's list, and what pressing it does
        protected PanelEntry AddEntry(string label, UnityEngine.Events.UnityAction onClick,
            bool interactable = true)
        {
            if (entryPrefab == null || itemsParent == null)
            {
                Missing("Entry Prefab");
                return null;
            }
            PanelEntry entry = Instantiate(entryPrefab, itemsParent);
            entry.SetLabel(label);
            if (entry.Button != null)
            {
                entry.Button.interactable = interactable;
                if (onClick != null)
                {
                    entry.Button.onClick.AddListener(onClick);
                }
            }
            return entry;
        }

        //a line in the list that is not pressable: an instruction, an answer, an empty state
        protected TextMeshProUGUI AddText(string text, PanelText voice)
        {
            TextMeshProUGUI prefab = PrefabFor(voice);
            if (prefab == null || itemsParent == null)
            {
                Missing(voice + " Text Prefab");
                return null;
            }
            TextMeshProUGUI line = Instantiate(prefab, itemsParent);
            line.text = text;
            return line;
        }

        private TextMeshProUGUI PrefabFor(PanelText voice)
        {
            switch (voice)
            {
                case PanelText.Dim:
                    return dimTextPrefab;
                case PanelText.Note:
                    return noteTextPrefab;
                default:
                    return bodyTextPrefab;
            }
        }

        //Nothing here can be put together from scratch any more, so a screen missing a piece says which
        //piece and which screen, once, rather than throwing the same null every time a row is added.
        protected void Missing(string field)
        {
            if (complained)
            {
                return;
            }
            complained = true;
            Debug.LogWarning(GetType().Name + " has no " + field + " assigned, so it has nothing to "
                + "show. Wire it up on the " + GetType().Name + " component.", this);
        }
    }
}
