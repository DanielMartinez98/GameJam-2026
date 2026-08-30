using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //The interrogation room is five things the player can press, and this is what sits behind all five.
    //
    //  the notebook   writes clues from premade sentences
    //  the phone      puts one of those clues to one of the suspects
    //  the case file  the police report: a page per suspect, then the autopsy
    //  the detective  goes back into one of the three memories, from the start
    //  the memory     goes back into the memory left half served, if there is one
    //
    //The first three open a screen over the room. The last two leave the scene for the dining room, so
    //what they are actually doing is writing down which memory to start and loading it; the memory in
    //progress lives in CaseFile, which is the only thing that survives the trip.
    //
    //All the case's writing is authored here in the scene, next to the memories it is about, the same
    //way the food orders are.
    public class InterrogationRoomDirector : MonoBehaviour
    {
        //Each thing in the room carries the name the player is shown when the cursor is over it. They are
        //authored rather than taken from the GameObject, which is named for the scene tree.
        [Header("Room buttons")]
        [SerializeField] private Button notebookButton;
        [SerializeField] private string notebookName = "Notebook";
        [SerializeField] private Button phoneButton;
        [SerializeField] private string phoneName = "Phone";
        [SerializeField] private Button caseInformationButton;
        [SerializeField] private string caseInformationName = "Case file";
        [SerializeField] private Button playerCharacterButton;
        [SerializeField] private string playerCharacterName = "Testimony";
        //Only in the room at all when a memory was walked out of half served. There is no "no memory"
        //state to dress, because in that state the button is not on the table to be looked at.
        [SerializeField] private Button currentMemoryButton;
        [SerializeField] private string currentMemoryName = "Current memory";
        //the memory button says which memory it would take you back to, if it has somewhere to write that
        [SerializeField] private TMPro.TextMeshProUGUI currentMemoryLabel;

        [Header("Hover name")]
        //Where the hovered object's name is written: the text inside the little pill on the canvas. Its
        //parent is the pill itself, which is what gets shown, hidden and moved, so the two have to stay
        //that way round. Left empty, the room simply does not name what the cursor is over.
        [SerializeField] private TMPro.TextMeshProUGUI hoverLabel;
        //clear of the cursor rather than under it, so the pointer is never covering its own label
        [SerializeField] private Vector2 hoverLabelOffset = new Vector2(18f, 18f);

        [Header("Screens")]
        [SerializeField] private NotebookPanel notebookPanel;
        [SerializeField] private PhonePanel phonePanel;
        [SerializeField] private CaseInformationPanel caseInformationPanel;
        [SerializeField] private MemorySelectPanel memorySelectPanel;
        //Plays the picture-and-prologue on the way into a memory. Left empty, one is made here the same
        //way the screens are; a memory with no prologue drops straight into the dining room regardless.
        [SerializeField] private CutsceneDirector cutsceneDirector;
        //what a screen is built into when it has no card of its own wired up. Left empty, the canvas the
        //buttons are already on is used, which is where they belong anyway.
        [SerializeField] private Transform panelParent;

        //Everything below is filled in when the component is first added and is meant to be written over
        //in the inspector - it is the shape of the case rather than the case. The suspects are the six
        //who are actually in the memories, matched to their prefabs by name so the two halves of the
        //game are talking about the same people; their details and alibis are the writing still to do.
        [Header("The case")]
        [SerializeField] private SuspectProfile[] suspects = new SuspectProfile[]
        {
            new SuspectProfile { displayName = "The Baron", prefabName = "Baron", phoneNumber = "555-0101" },
            new SuspectProfile { displayName = "The Chief", prefabName = "Chief", phoneNumber = "555-0102" },
            new SuspectProfile { displayName = "The Henchman", prefabName = "Henchman", phoneNumber = "555-0103" },
            new SuspectProfile { displayName = "The Mayor", prefabName = "Mayor", phoneNumber = "555-0104" },
            new SuspectProfile { displayName = "The Pianist", prefabName = "Pianist", phoneNumber = "555-0105" },
            new SuspectProfile { displayName = "The Widow", prefabName = "Widow", phoneNumber = "555-0106" },
            //the detective's own file: whoever is being asked all this is on the list of people it
            //could have been, which is the whole reason they are sitting in the room
            new SuspectProfile { displayName = "The Waiter", prefabName = "Player Character", phoneNumber = "555-0107", isPlayerCharacter = true }
        };
        //Two worked examples, so the notebook has something to write the day it is switched on and the
        //format is there to copy. The real set of sentences replaces them.
        [SerializeField] private ClueTemplate[] clueTemplates = new ClueTemplate[]
        {
            new ClueTemplate
            {
                templateId = "allergy",
                menuLabel = "... is allergic to ...",
                sentence = "{0} is allergic to {1}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot { kind = ClueSlotKind.Food }
                }
            },
            new ClueTemplate
            {
                templateId = "served",
                menuLabel = "... was served ...",
                sentence = "{0} was served {1}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot { kind = ClueSlotKind.Food }
                }
            },
            new ClueTemplate
            {
                templateId = "hurt",
                menuLabel = "... hurt ...",
                sentence = "{0} hurt {1}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot { kind = ClueSlotKind.Suspect }
                }
            },
            new ClueTemplate
            {
                templateId = "hadOn",
                menuLabel = "... had ... on them",
                sentence = "{0} had {1} on them",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "What did they have on them?",
                        customOptions = new string[]
                        {
                            "the victim's ring", "cuts on his hands", "a bloodstained apron",
                            "a gold pocket watch", "muddy boots", "a torn jacket sleeve",
                            "the victim's cufflinks"
                        }
                    }
                }
            },
            new ClueTemplate
            {
                templateId = "ateAt",
                menuLabel = "... ate ... at ...",
                sentence = "{0} ate {1} at {2}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot { kind = ClueSlotKind.Food },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "At what time?",
                        customOptions = new string[]
                        {
                            "8 PM", "9 PM", "10 PM", "11 PM", "12 AM", "1 AM"
                        }
                    }
                }
            },
            new ClueTemplate
            {
                templateId = "ordered",
                menuLabel = "... ordered ... at ...",
                sentence = "{0} ordered {1} at {2}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot { kind = ClueSlotKind.Food },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "At what time?",
                        customOptions = new string[]
                        {
                            "8 PM", "9 PM", "10 PM", "11 PM", "12 AM", "1 AM"
                        }
                    }
                }
            },
            new ClueTemplate
            {
                templateId = "hadAt",
                menuLabel = "... had ... at ...",
                sentence = "{0} had {1} at {2}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "What did they have?",
                        customOptions = new string[]
                        {
                            "the victim's ring", "cuts on his hands", "a bloodstained apron",
                            "a gold pocket watch", "muddy boots", "a torn jacket sleeve",
                            "the victim's cufflinks"
                        }
                    },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "At what time?",
                        customOptions = new string[]
                        {
                            "8 PM", "9 PM", "10 PM", "11 PM", "12 AM", "1 AM"
                        }
                    }
                }
            },
            new ClueTemplate
            {
                templateId = "took",
                menuLabel = "... took ... at ...",
                sentence = "{0} took {1} at {2}",
                slots = new ClueSlot[]
                {
                    new ClueSlot { kind = ClueSlotKind.Suspect },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "What did they take?",
                        customOptions = new string[]
                        {
                            "the knife", "a wine glass", "a candlestick", "the victim's ring",
                            "a napkin", "a serving tray"
                        }
                    },
                    new ClueSlot
                    {
                        kind = ClueSlotKind.Custom,
                        prompt = "At what time?",
                        customOptions = new string[]
                        {
                            "8 PM", "9 PM", "10 PM", "11 PM", "12 AM", "1 AM"
                        }
                    }
                }
            }
        };
        //what a Food slot offers. These are the food tags the dining room serves, so a clue about a food
        //names the same thing the player put on the plate.
        [SerializeField] private string[] foodOptions = new string[]
        {
            "BlueBerries", "Cheese", "Cracker", "Garnish", "Grapes", "Nuts", "Oranges", "Pickles",
            "Salami", "Strawberry"
        };
        //the pages of the file that are not people: the autopsy report, and anything else
        [SerializeField] private CasePage[] casePages = new CasePage[]
        {
            new CasePage { title = "Autopsy report", body = "" }
        };
        //the three nights, in the order GameDirectorMemories has them
        [SerializeField] private MemoryOption[] memories = new MemoryOption[]
        {
            new MemoryOption { title = "The first memory", memoryIndex = 0 },
            new MemoryOption { title = "The second memory", memoryIndex = 1 },
            new MemoryOption { title = "The third memory", memoryIndex = 2 }
        };

        [Header("The dining room")]
        [SerializeField] private string memorySceneName = "GameScene";

        [Header("Development")]
        //Every memory counts as finished, so every memory can be walked into without playing the one
        //before it. On while the game is being built; turn it off before anyone else plays it, or the
        //whole case is open from the first minute. It changes nothing on disk, so switching it off
        //gives back whatever progress was really made.
        [SerializeField] private bool unlockAllMemories = true;

        //whichever screen is up, or null when the player is looking at the room
        private RoomPanel openPanel;

        public SuspectProfile[] Suspects { get { return suspects; } }
        public ClueTemplate[] ClueTemplates { get { return clueTemplates; } }
        public CasePage[] CasePages { get { return casePages; } }
        public MemoryOption[] Memories { get { return memories; } }

        public Transform PanelParent
        {
            get
            {
                if (panelParent != null)
                {
                    return panelParent;
                }
                //the buttons are on the canvas the screens should cover, so it is found from one of them
                //rather than having to be wired up a second time
                Button anyButton = notebookButton != null ? notebookButton : phoneButton;
                Canvas canvas = anyButton != null
                    ? anyButton.GetComponentInParent<Canvas>()
                    : FindFirstObjectByType<Canvas>();
                return canvas != null ? canvas.transform : null;
            }
        }

        private void Start()
        {
            CaseFile.UnlockEverything = unlockAllMemories;
            if (unlockAllMemories)
            {
                //said out loud every run, because the one way this setting goes wrong is nobody
                //noticing it is still on
                Debug.LogWarning("Interrogation room: every memory is unlocked for development. "
                    + "Turn off Unlock All Memories on the director to play the case properly.", this);
            }

            //The screens are behaviour, not scenery: there is nothing to place, nothing to position and
            //one of each. Leaving them off the director is the normal case, so they are made here rather
            //than being four more objects to remember to drag in. Wiring one up by hand still wins.
            notebookPanel = EnsurePanel(notebookPanel);
            phonePanel = EnsurePanel(phonePanel);
            caseInformationPanel = EnsurePanel(caseInformationPanel);
            memorySelectPanel = EnsurePanel(memorySelectPanel);
            //the cutscene is behaviour with no card of its own, so like the screens it is made here when
            //one has not been wired up by hand
            if (cutsceneDirector == null)
            {
                cutsceneDirector = gameObject.AddComponent<CutsceneDirector>();
            }

            HookUp(notebookButton, OpenNotebook);
            HookUp(phoneButton, OpenPhone);
            HookUp(caseInformationButton, OpenCaseInformation);
            HookUp(playerCharacterButton, OpenMemorySelect);
            HookUp(currentMemoryButton, ResumeMemory);

            NameIt(notebookButton, notebookName);
            NameIt(phoneButton, phoneName);
            NameIt(caseInformationButton, caseInformationName);
            NameIt(playerCharacterButton, playerCharacterName);
            NameIt(currentMemoryButton, currentMemoryName);

            ClosePanels();
        }

        //gives one thing in the room a name to show while the cursor is on it
        private void NameIt(Button button, string displayName)
        {
            if (button == null || string.IsNullOrEmpty(displayName))
            {
                return;
            }
            RoomHotspot hotspot = button.GetComponent<RoomHotspot>();
            if (hotspot == null)
            {
                hotspot = button.gameObject.AddComponent<RoomHotspot>();
            }
            hotspot.Bind(this, displayName);
        }

        private T EnsurePanel<T>(T panel) where T : RoomPanel
        {
            return panel != null ? panel : gameObject.AddComponent<T>();
        }

        private static void HookUp(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }
            //added rather than assigned, so anything already wired up in the inspector still fires
            button.onClick.AddListener(action);
        }

        //The room only ever shows one screen at a time. Pressing the object a screen already belongs to
        //puts it away again, so the notebook closes the same way it opened.
        private void OpenPanel(RoomPanel panel)
        {
            if (panel == null)
            {
                Debug.LogWarning("That part of the room has no screen assigned on the director.");
                return;
            }
            if (openPanel == panel)
            {
                ClosePanels();
                return;
            }
            ClosePanels();
            openPanel = panel;
            //the cursor is still sitting on whatever was just pressed, and it is about to be under the
            //screen that press opened, so its name goes away with the room
            HideHoverName(null);
            panel.Open(this);
            RefreshRoomButtons();
        }

        public void ClosePanels()
        {
            if (openPanel != null)
            {
                openPanel.Close();
                openPanel = null;
            }
            RefreshRoomButtons();
        }

        public void OpenNotebook()
        {
            OpenPanel(notebookPanel);
        }

        public void OpenPhone()
        {
            OpenPanel(phonePanel);
        }

        public void OpenCaseInformation()
        {
            OpenPanel(caseInformationPanel);
        }

        public void OpenMemorySelect()
        {
            OpenPanel(memorySelectPanel);
        }

        //The memory button is the only one in the room that can have nothing to do. It is switched off
        //rather than hidden, so the player can see it is there and learn what fills it in.
        private void RefreshRoomButtons()
        {
            bool hasMemory = CaseFile.UnfinishedMemory >= 0;
            //while a screen is up, the table behind it is out of reach
            bool roomIsClear = openPanel == null;

            //With nothing left half served there is no memory to go back to, so the button comes off the
            //table entirely - art and all - rather than sitting there greyed out. Having one in progress
            //is the exception rather than the normal state of the room, and a button that is dead for
            //most of the game reads as something broken instead of as something not yet earned.
            if (currentMemoryButton != null)
            {
                currentMemoryButton.gameObject.SetActive(hasMemory);
                //when it is there at all it behaves like the rest of the table: still shown while a
                //screen is up, just not pressable through it
                currentMemoryButton.interactable = roomIsClear;
            }
            //hidden along with the button, for the case where it is a label beside it rather than on it
            if (currentMemoryLabel != null)
            {
                currentMemoryLabel.gameObject.SetActive(hasMemory);
                if (hasMemory)
                {
                    currentMemoryLabel.text = TitleOf(CaseFile.UnfinishedMemory);
                }
            }

            SetInteractable(notebookButton, roomIsClear || openPanel == (RoomPanel)notebookPanel);
            SetInteractable(phoneButton, roomIsClear || openPanel == (RoomPanel)phonePanel);
            SetInteractable(caseInformationButton, roomIsClear || openPanel == (RoomPanel)caseInformationPanel);
            SetInteractable(playerCharacterButton, roomIsClear || openPanel == (RoomPanel)memorySelectPanel);
        }

        //The name follows the cursor, so it is only worth any work while one is actually being shown.
        private void Update()
        {
            if (hoveredSpot != null)
            {
                PlaceHoverLabel();
            }
        }

        //Which hotspot the name currently belongs to. Held so a stale exit - the cursor having already
        //moved on to the next object by the time the last one reports leaving - cannot wipe out the name
        //that has just replaced it.
        private RoomHotspot hoveredSpot;

        public void ShowHoverName(RoomHotspot spot, string displayName)
        {
            //with a screen up, the room behind it is not being pointed at even where it can still be
            //touched by the cursor, and naming what is under there would be describing the wrong thing
            if (openPanel != null)
            {
                return;
            }
            hoveredSpot = spot;
            if (hoverLabel == null)
            {
                return;
            }
            hoverLabel.text = displayName;
            RectTransform pill = HoverPill;
            pill.gameObject.SetActive(true);
            //drawn after everything else on the canvas, or the table art it is sitting over covers it
            pill.SetAsLastSibling();
            //the pill measures itself from the new text, and it has to have done so before it is placed,
            //or this frame's position is worked out against the last name's width
            LayoutRebuilder.ForceRebuildLayoutImmediate(pill);
            PlaceHoverLabel();
        }

        //Ignored unless the hotspot leaving is the one the name is actually about.
        public void HideHoverName(RoomHotspot spot)
        {
            if (spot != null && hoveredSpot != spot)
            {
                return;
            }
            hoveredSpot = null;
            if (hoverLabel != null)
            {
                HoverPill.gameObject.SetActive(false);
            }
        }

        //the pill is the label's parent, since the label is the text inside it
        private RectTransform HoverPill
        {
            get { return (RectTransform)hoverLabel.transform.parent; }
        }

        private void PlaceHoverLabel()
        {
            if (hoverLabel == null)
            {
                return;
            }
            RectTransform pill = HoverPill;
            RectTransform canvasRect = pill.parent as RectTransform;
            if (canvasRect == null)
            {
                return;
            }
            //An overlay canvas is drawn without a camera and wants a null one here; anything else is
            //drawn through its own. Passing the wrong one puts the name somewhere across the screen
            //from the cursor, so it is taken from the canvas rather than assumed.
            Canvas canvas = pill.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition,
                camera, out local))
            {
                return;
            }
            local += hoverLabelOffset;
            //Kept on screen. The pill grows up and to the right of this point, so near the top or the
            //right edge it would otherwise run off, which is exactly where the objects on the table are.
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 size = pill.rect.size;
            local.x = Mathf.Clamp(local.x, -half.x, Mathf.Max(-half.x, half.x - size.x));
            local.y = Mathf.Clamp(local.y, -half.y, Mathf.Max(-half.y, half.y - size.y));
            pill.anchoredPosition = local;
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private string TitleOf(int memoryIndex)
        {
            if (memories != null)
            {
                foreach (MemoryOption option in memories)
                {
                    if (option != null && option.memoryIndex == memoryIndex)
                    {
                        return option.title;
                    }
                }
            }
            return "Memory " + (memoryIndex + 1);
        }

        //Picking a memory from the detective starts it over and drops whatever was saved; the memory
        //button picks up exactly where it was left, so it keeps it.
        public void EnterMemory(int memoryIndex, bool startOver)
        {
            ClosePanels();
            CaseFile.RequestMemory(memoryIndex, startOver);
            if (string.IsNullOrEmpty(memorySceneName))
            {
                Debug.LogError("No dining room scene set on the director, so there is nowhere to go back to.");
                return;
            }
            //The memory's prologue plays first when it has one, and the dining room is loaded only once
            //the player presses on; with no prologue there is nothing to hold on and it loads at once.
            MemoryOption option = OptionFor(memoryIndex);
            if (cutsceneDirector != null && option != null && option.HasPrologue)
            {
                cutsceneDirector.Play(option.prologueImage, option.prologueText, LoadMemoryScene);
                return;
            }
            LoadMemoryScene();
        }

        private void LoadMemoryScene()
        {
            SceneManager.LoadScene(memorySceneName);
        }

        //the authored memory for an index, or null when nothing lines up with it
        private MemoryOption OptionFor(int memoryIndex)
        {
            if (memories != null)
            {
                foreach (MemoryOption option in memories)
                {
                    if (option != null && option.memoryIndex == memoryIndex)
                    {
                        return option;
                    }
                }
            }
            return null;
        }

        public void ResumeMemory()
        {
            int memoryIndex = CaseFile.UnfinishedMemory;
            if (memoryIndex < 0)
            {
                return;
            }
            EnterMemory(memoryIndex, false);
        }

        //How many memories the case has, which is what "all of them" is measured against.
        public int MemoryCount
        {
            get { return memories != null ? memories.Length : 0; }
        }

        //Every memory can be entered. This is what opens the accusation up: the detective has been
        //everywhere there is to go, whether or not they served everyone once they got there.
        public bool AllMemoriesUnlocked
        {
            get { return CaseFile.AllMemoriesUnlocked(MemoryCount); }
        }

        //The stricter version: everyone in every memory has been served.
        public bool AllMemoriesCompleted
        {
            get { return CaseFile.AllMemoriesCompleted(MemoryCount); }
        }

        //The earliest memory still holding a clue the player has not written down, or -1 when the case
        //is complete. Earliest rather than emptiest, so the player is sent back to the start of what
        //they have missed rather than past it.
        public int MemoryMissingClues
        {
            get { return CorrectClues.FirstMemoryMissingClues(); }
        }

        //one line saying where to look next, ready to put on screen
        public string MissingCluesMessage
        {
            get { return CorrectClues.DescribeMissing(); }
        }

        //The case now outlives the run, which is the point of saving it and also the thing that makes
        //progression impossible to test: once the memories are unlocked they stay unlocked. This puts
        //the case back to its first minute.
        [ContextMenu("Clear Saved Case")]
        private void ClearSavedCase()
        {
            CaseFile.Reset();
            Debug.Log("Saved case cleared: no clues, no memories completed.\n" + CaseFile.SaveFilePath, this);
            RefreshRoomButtons();
        }

        //The answers are read once and kept, so editing them while the game is running otherwise has no
        //effect until the next play.
        [ContextMenu("Reload Correct Clues")]
        private void ReloadCorrectClues()
        {
            CorrectClues.Reload();
            Debug.Log("Correct clues reloaded. " + CorrectClues.DescribeMissing(), this);
        }

        //The clue keys the suspects' responses have to be written against. There is no guessing them by
        //hand: this walks every template against every pick it can be filled in with and prints the lot,
        //ready to be pasted into a response's Clue Key.
        [ContextMenu("List Clue Keys")]
        private void ListClueKeys()
        {
            if (clueTemplates == null)
            {
                return;
            }
            System.Text.StringBuilder keys = new System.Text.StringBuilder("Clue keys for this case:\n");
            foreach (ClueTemplate template in clueTemplates)
            {
                if (template != null)
                {
                    AppendKeys(keys, template, new string[template.slots != null ? template.slots.Length : 0], 0);
                }
            }
            Debug.Log(keys.ToString(), this);
        }

        //one pick at a time, down to the end of the sentence and back up again for the next pick
        private void AppendKeys(System.Text.StringBuilder keys, ClueTemplate template, string[] picks, int slotIndex)
        {
            if (slotIndex >= picks.Length)
            {
                keys.Append("  ").Append(CaseFile.BuildKey(template.templateId, picks)).Append('\n');
                return;
            }
            string[] options = OptionsFor(template.slots[slotIndex]);
            if (options == null || options.Length == 0)
            {
                return;
            }
            foreach (string option in options)
            {
                picks[slotIndex] = option;
                AppendKeys(keys, template, picks, slotIndex + 1);
            }
        }

        //What a slot offers the player. Suspects and foods are the case's own lists, so a template
        //saying "{0} is allergic to {1}" needs nothing filled in on it; anything else brings its own.
        public string[] OptionsFor(ClueSlot slot)
        {
            if (slot == null)
            {
                return null;
            }
            switch (slot.kind)
            {
                case ClueSlotKind.Suspect:
                    return SuspectNames();
                case ClueSlotKind.Food:
                    return foodOptions;
                default:
                    return slot.customOptions;
            }
        }

        private string[] SuspectNames()
        {
            if (suspects == null)
            {
                return null;
            }
            string[] names = new string[suspects.Length];
            for (int i = 0; i < suspects.Length; i++)
            {
                names[i] = suspects[i] != null ? suspects[i].displayName : string.Empty;
            }
            return names;
        }
    }
}
