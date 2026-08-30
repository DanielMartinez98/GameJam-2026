using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //Where clues are written. The player never types anything: they pick one of the premade sentences,
    //then fill its blanks one at a time - which character, then which food - and the finished sentence
    //goes in the book. That is the whole of it, and it is deliberately the only way a clue can come into
    //existence, so the phone can be certain every clue it is handed is one the responses were written
    //against.
    public class NotebookPanel : RoomPanel
    {
        //the sentence being filled in, or null while the player is still choosing which one to write
        private ClueTemplate draftTemplate;
        //what has been picked for it so far, one entry per slot already answered
        private readonly List<string> draftPicks = new List<string>();
        //shown under the column after a clue is written, cleared the moment anything else is pressed
        private string notice;
        //true while a clue is being struck through, so nothing else on the page is pressed into a
        //Refresh that would tear the animating row out from under the coroutine
        private bool scratching;
        //true while the book is turned to the pages of written clues rather than the sentences to write
        private bool viewingClues;
        //which page of written clues is showing. The pages are made as they are needed, so there is no
        //fixed number of them: a book that has filled one page simply has a second, and so on.
        private int cluePage;
        //how many written lines a page of the book holds before the next one is started
        private const int CluesPerPage = 10;

        //A clue that turned out to be part of the solution reads green; a special clue the phone turned
        //up reads yellow. Written straight into the line as a rich-text colour so the same Note prefab
        //carries all three without a prefab per colour.
        private const string CorrectColor = "#2E7D32";
        private const string SpecialColor = "#C8A415";

        private static string Colored(string text, string hex)
        {
            return "<color=" + hex + ">" + text + "</color>";
        }

        protected override string Title
        {
            get
            {
                //the pages of written clues, numbered so the player can see there are more behind this one
                if (viewingClues)
                {
                    return "Notebook - Written down (" + (cluePage + 1) + "/" + CluePageCount + ")";
                }
                if (draftTemplate == null)
                {
                    return "Notebook";
                }
                //the sentence so far, with the blank being filled left showing as a blank
                return "Notebook - " + Preview(draftTemplate, draftPicks, true);
            }
        }

        protected override void OnOpened()
        {
            //the card was drawn without a forward arrow, so the book grows its own the first time it is
            //opened - the green one at the corner that turns to the written pages
            EnsureForwardArrow();
            //a half written clue left over from last time would have the player finishing a sentence
            //they have forgotten starting
            draftTemplate = null;
            draftPicks.Clear();
            notice = null;
            scratching = false;
            //the book is always opened to the sentences to write, not to a page of clues left showing
            viewingClues = false;
            cluePage = 0;
        }

        //A card drawn without a forward arrow gets one made from its own back arrow: the same drawn
        //shape, turned to point the other way, tinted green and stood in the opposite bottom corner, so
        //there is always a plain way to turn to the written pages without hunting down a scroll.
        private void EnsureForwardArrow()
        {
            if (forwardButton != null || backButton == null)
            {
                return;
            }
            Button arrow = Instantiate(backButton, backButton.transform.parent);
            arrow.name = "Forward";
            RectTransform rect = arrow.transform as RectTransform;
            RectTransform from = backButton.transform as RectTransform;
            if (rect != null && from != null)
            {
                //the card is turned on its side, so the back arrow's own frame is reused and only the
                //corner and the direction it points are changed. Its local x holds the bottom edge; a
                //negative local y is what stands it at the right of the card rather than the left.
                rect.anchoredPosition = new Vector2(from.anchoredPosition.x, -300f);
                rect.localRotation = from.localRotation * Quaternion.Euler(0f, 0f, 180f);
                rect.localScale = from.localScale;
            }
            Image graphic = arrow.GetComponent<Image>();
            if (graphic != null)
            {
                //a bright, obvious green so it reads as the way on rather than as part of the drawn book
                graphic.color = new Color(0.298f, 0.686f, 0.314f);
            }
            arrow.onClick.AddListener(OnForwardArrow);
            forwardButton = arrow;
        }

        //the green arrow: on into the written pages from the sentences, then page by page through them
        private void OnForwardArrow()
        {
            if (CanGoForward)
            {
                GoForward();
            }
        }

        protected override void Populate()
        {
            if (viewingClues)
            {
                PopulateCluesPage();
            }
            else if (draftTemplate == null)
            {
                PopulateTemplateList();
            }
            else
            {
                PopulateSlotOptions();
            }
        }

        //The drawn arrow steps back through whatever the book is showing: a page of clues back to the
        //one before it and then to the sentences, or a half written sentence back a blank at a time.
        protected override bool CanGoBack
        {
            get { return viewingClues || draftTemplate != null; }
        }

        protected override void GoBack()
        {
            if (scratching)
            {
                return;
            }
            if (viewingClues)
            {
                if (cluePage > 0)
                {
                    cluePage--;
                    Refresh();
                }
                else
                {
                    ReturnToWriting();
                }
                return;
            }
            Back();
        }

        //Forward turns to the written pages from the sentences, and then on through them. It stops at
        //the back of the book and offers nothing while a sentence is half written.
        protected override bool CanGoForward
        {
            get
            {
                if (viewingClues)
                {
                    return cluePage < CluePageCount - 1;
                }
                return draftTemplate == null && ReadbackCount > 0;
            }
        }

        protected override void GoForward()
        {
            if (scratching)
            {
                return;
            }
            if (viewingClues)
            {
                TurnCluePage(1);
                return;
            }
            //from the sentences the arrow turns to the front of the written pages
            OpenClues();
        }

        //the sentences on offer, and under them everything already written down
        private void PopulateTemplateList()
        {
            ClueTemplate[] templates = director != null ? director.ClueTemplates : null;
            if (templates == null || templates.Length == 0)
            {
                AddText("No clue templates have been written yet.", PanelText.Dim);
                return;
            }
            //said at the top where it is seen without scrolling, so the green arrow's job is plain
            int written = ReadbackCount;
            if (written > 0)
            {
                AddText(Colored(written + " written down - turn the green arrow \u203A", CorrectColor),
                    PanelText.Note);
            }
            AddText("Pick a sentence to write:", PanelText.Dim);
            foreach (ClueTemplate template in templates)
            {
                if (template == null)
                {
                    continue;
                }
                //captured into a local, or every row would end up writing the last template in the array
                ClueTemplate chosen = template;
                AddEntry(template.menuLabel, delegate { BeginTemplate(chosen); });
            }
            if (notice != null)
            {
                AddText(notice, PanelText.Note);
            }
        }

        //The pages of everything already written down: the clues the player wrote, then the findings the
        //phone turned up, a page at a time. A page is filled and the next one is begun, so the book
        //grows a page whenever it needs one rather than scrolling on forever.
        private void PopulateCluesPage()
        {
            //the card's own arrow already steps back out of these pages, so it is not offered twice
            if (!HasBackButton)
            {
                AddEntry("< Back", ReturnToWriting);
            }
            int total = ReadbackCount;
            if (total == 0)
            {
                AddText("Nothing is written down yet.", PanelText.Dim);
                return;
            }
            //a page crossed empty by striking off its last clue would sit past the end of the book
            cluePage = Mathf.Clamp(cluePage, 0, CluePageCount - 1);
            int start = cluePage * CluesPerPage;
            int end = Mathf.Min(start + CluesPerPage, total);
            for (int i = start; i < end; i++)
            {
                RenderReadback(i);
            }
        }

        //One line of the written-down pages by its place in the book: the player's own clues first, then
        //the phone's findings after them.
        private void RenderReadback(int index)
        {
            IList<CaseFile.Clue> clues = CaseFile.Clues;
            if (index < clues.Count)
            {
                AddClueRow(clues[index]);
                return;
            }
            IList<CaseFile.SpecialClue> special = CaseFile.SpecialClues;
            int specialIndex = index - clues.Count;
            if (specialIndex >= 0 && specialIndex < special.Count)
            {
                //the phone's findings, read back but never struck off: the player did not write these,
                //the case did, so they are not theirs to tidy away
                AddText("  " + Colored("\u2022 " + special[specialIndex].text, SpecialColor), PanelText.Note);
            }
        }

        //One clue the player wrote, drawn as its own line: green and kept if it turned out to be part of
        //the solution, struck if it has already broken a suspect, and otherwise a button that crosses
        //it off.
        private void AddClueRow(CaseFile.Clue clue)
        {
            CaseFile.Clue chosen = clue;
            //a clue that has already broken a suspect is still worth reading back, so it stays in the
            //book and is only marked as spent
            string line = clue.used ? "<s>" + clue.text + "</s>" : clue.text;
            if (clue.IsCorrect)
            {
                //Part of the solution, so there is no crossing it out. Marked in green instead, so the
                //player can see which of their guesses have turned out to be worth something.
                AddText("  " + Colored("\u2022 " + line, CorrectColor), PanelText.Note);
                return;
            }
            //a guess that led nowhere can be struck off, and the row itself is the button that does it
            PanelEntry row = null;
            row = AddEntry("  " + line, delegate { CrossOut(chosen, row); });
        }

        //how many lines there are to read back: the player's clues and the phone's findings together
        private int ReadbackCount
        {
            get { return CaseFile.Clues.Count + CaseFile.SpecialClues.Count; }
        }

        //as many pages as it takes to hold them all, and always at least the one
        private int CluePageCount
        {
            get { return Mathf.Max(1, (ReadbackCount + CluesPerPage - 1) / CluesPerPage); }
        }

        //turn the book to the pages of written clues, at the front of them
        private void OpenClues()
        {
            if (scratching)
            {
                return;
            }
            viewingClues = true;
            cluePage = 0;
            notice = null;
            Refresh();
        }

        //close the book on the clues and go back to the sentences to write
        private void ReturnToWriting()
        {
            viewingClues = false;
            cluePage = 0;
            Refresh();
        }

        //a page turned in the written clues, stopping at either end rather than wrapping round
        private void TurnCluePage(int step)
        {
            if (scratching)
            {
                return;
            }
            cluePage = Mathf.Clamp(cluePage + step, 0, CluePageCount - 1);
            Refresh();
        }

        //the picks for whichever blank is next in the sentence being written
        private void PopulateSlotOptions()
        {
            int slotIndex = draftPicks.Count;
            ClueSlot slot = slotIndex < draftTemplate.slots.Length ? draftTemplate.slots[slotIndex] : null;
            //the page's own arrow already says this, so it is not said twice
            if (!HasBackButton)
            {
                AddEntry("< Back", Back);
            }
            if (slot == null)
            {
                //an empty entry in the template's slot array. Nothing can be picked for it, so the
                //sentence cannot be finished and the player is told rather than left pressing nothing.
                AddText("This clue template has an empty slot and cannot be completed.", PanelText.Dim);
                return;
            }
            string[] options = director.OptionsFor(slot);
            if (options == null || options.Length == 0)
            {
                AddText("Nothing to pick here - this slot has no options set up.", PanelText.Dim);
                return;
            }
            AddText(SlotPrompt(slot), PanelText.Dim);
            foreach (string option in options)
            {
                string chosen = option;
                AddEntry(chosen, delegate { Pick(chosen); });
            }
        }

        private string SlotPrompt(ClueSlot slot)
        {
            //a custom slot names its own question, since "which one?" reads oddly for a time or object
            if (!string.IsNullOrEmpty(slot.prompt))
            {
                return slot.prompt;
            }
            switch (slot.kind)
            {
                case ClueSlotKind.Suspect:
                    return "Which character?";
                case ClueSlotKind.Food:
                    return "Which food?";
                default:
                    return "Which one?";
            }
        }

        //A wrong guess taken back out of the book. Refused for anything that turns out to be part of the
        //solution, which the book has already marked as such, so this is only ever reached for a guess.
        private void CrossOut(CaseFile.Clue clue, PanelEntry row)
        {
            //one at a time: the animating row is about to be redrawn away, so a second scratch would be
            //working on a torn out label
            if (scratching)
            {
                return;
            }
            //part of the solution: the book has already marked it and there is no striking it off
            if (clue.IsCorrect)
            {
                notice = "That one is worth keeping.";
                Refresh();
                return;
            }
            //no row to animate (nothing to scratch), so it just comes straight out
            if (row == null || row.Label == null)
            {
                notice = CaseFile.RemoveClue(clue.Key) ? "Crossed out." : "That one is worth keeping.";
                Refresh();
                return;
            }
            StartCoroutine(ScratchOut(clue, row));
        }

        //The pencil goes back and forth: the word is struck through a little further each frame, held a
        //beat once it is fully crossed, then faded off the page before the clue is actually taken out of
        //the book and the list is drawn again without it.
        private IEnumerator ScratchOut(CaseFile.Clue clue, PanelEntry row)
        {
            scratching = true;
            if (row.Button != null)
            {
                row.Button.interactable = false;
            }
            TextMeshProUGUI label = row.Label;
            string word = clue.text;
            int length = word.Length;
            const float scratchTime = 0.45f;
            float elapsed = 0f;
            while (elapsed < scratchTime)
            {
                elapsed += Time.unscaledDeltaTime;
                int struck = Mathf.Clamp(Mathf.RoundToInt(length * (elapsed / scratchTime)), 0, length);
                label.text = "  <s>" + word.Substring(0, struck) + "</s>" + word.Substring(struck);
                yield return null;
            }
            label.text = "  <s>" + word + "</s>";
            //a held beat on the finished strike, then it fades
            const float fadeTime = 0.3f;
            float startAlpha = label.alpha;
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                label.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeTime);
                yield return null;
            }
            CaseFile.RemoveClue(clue.Key);
            notice = "Crossed out.";
            scratching = false;
            Refresh();
        }

        private void BeginTemplate(ClueTemplate template)
        {
            //not while a clue is mid-scratch: choosing a sentence redraws the page and would tear the
            //animating row out from under the coroutine
            if (scratching)
            {
                return;
            }
            draftTemplate = template;
            draftPicks.Clear();
            notice = null;
            //a sentence with no blanks is a clue on its own and is written as soon as it is picked
            if (template.slots == null || template.slots.Length == 0)
            {
                CommitDraft();
                return;
            }
            Refresh();
        }

        private void Pick(string option)
        {
            draftPicks.Add(option);
            if (draftPicks.Count >= draftTemplate.slots.Length)
            {
                CommitDraft();
                return;
            }
            Refresh();
        }

        //one blank at a time, and backing out of the first one puts the sentence itself back on the table
        private void Back()
        {
            if (draftPicks.Count > 0)
            {
                draftPicks.RemoveAt(draftPicks.Count - 1);
            }
            else
            {
                draftTemplate = null;
            }
            Refresh();
        }

        private void CommitDraft()
        {
            string text = Preview(draftTemplate, draftPicks, false);
            bool added = CaseFile.AddClue(draftTemplate.templateId, draftPicks.ToArray(), text);
            //kept short so it does not echo the clue that is already in the list right below it
            notice = added ? "Added to the book." : "That is already in the book.";
            draftTemplate = null;
            draftPicks.Clear();
            Refresh();
        }

        //Fills {0}, {1}, ... with what has been picked so far. While the sentence is still being written
        //the blanks left are shown as underscores, so the player can read what they are part way through
        //saying rather than a row of braces.
        private static string Preview(ClueTemplate template, List<string> picks, bool showBlanks)
        {
            string text = template.sentence;
            int slotCount = template.slots != null ? template.slots.Length : 0;
            for (int i = 0; i < slotCount; i++)
            {
                string value = i < picks.Count ? picks[i] : (showBlanks ? "____" : string.Empty);
                text = text.Replace("{" + i + "}", value);
            }
            return text;
        }
    }
}
