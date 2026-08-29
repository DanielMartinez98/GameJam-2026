using System.Collections.Generic;
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

        protected override string Title
        {
            get
            {
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
            //a half written clue left over from last time would have the player finishing a sentence
            //they have forgotten starting
            draftTemplate = null;
            draftPicks.Clear();
            notice = null;
        }

        protected override void Populate()
        {
            if (draftTemplate == null)
            {
                PopulateTemplateList();
            }
            else
            {
                PopulateSlotOptions();
            }
        }

        //the sentences on offer, and under them everything already written down
        private void PopulateTemplateList()
        {
            ClueTemplate[] templates = director != null ? director.ClueTemplates : null;
            if (templates == null || templates.Length == 0)
            {
                AddText("No clue templates have been written yet.", 22f, PanelUI.DimTextColor);
                return;
            }
            AddText("Pick a sentence to write:", 22f, PanelUI.DimTextColor);
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
                AddText(notice, 22f, PanelUI.HighlightColor);
            }
            PopulateWrittenClues();
        }

        private void PopulateWrittenClues()
        {
            IList<CaseFile.Clue> clues = CaseFile.Clues;
            if (clues.Count == 0)
            {
                return;
            }
            AddText("\nWritten down:", 22f, PanelUI.DimTextColor);
            //iterated over a copy, because crossing one out removes it from the list being walked
            List<CaseFile.Clue> written = new List<CaseFile.Clue>(clues);
            foreach (CaseFile.Clue clue in written)
            {
                CaseFile.Clue chosen = clue;
                //a clue that has already broken a suspect is still worth reading back, so it stays in
                //the book and is only marked as spent
                string line = clue.used ? "<s>" + clue.text + "</s>" : clue.text;
                if (clue.IsCorrect)
                {
                    //Part of the solution, so there is no crossing it out. It is marked instead: the
                    //player can see which of their guesses have turned out to be worth something.
                    AddText("  * " + line, 21f, PanelUI.HighlightColor);
                    continue;
                }
                //a guess that led nowhere can be struck off, and the row is the button that does it
                Button entry = AddEntry("  " + line + "      (cross out)", delegate { CrossOut(chosen); });
                PanelUI.SetEntryHeight(entry, entryHeight * 0.7f);
            }
        }

        //the picks for whichever blank is next in the sentence being written
        private void PopulateSlotOptions()
        {
            int slotIndex = draftPicks.Count;
            ClueSlot slot = slotIndex < draftTemplate.slots.Length ? draftTemplate.slots[slotIndex] : null;
            AddEntry("< Back", Back);
            if (slot == null)
            {
                //an empty entry in the template's slot array. Nothing can be picked for it, so the
                //sentence cannot be finished and the player is told rather than left pressing nothing.
                AddText("This clue template has an empty slot and cannot be completed.", 22f, PanelUI.DimTextColor);
                return;
            }
            string[] options = director.OptionsFor(slot);
            if (options == null || options.Length == 0)
            {
                AddText("Nothing to pick here - this slot has no options set up.", 22f, PanelUI.DimTextColor);
                return;
            }
            AddText(SlotPrompt(slot), 22f, PanelUI.DimTextColor);
            foreach (string option in options)
            {
                string chosen = option;
                AddEntry(chosen, delegate { Pick(chosen); });
            }
        }

        private string SlotPrompt(ClueSlot slot)
        {
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
        private void CrossOut(CaseFile.Clue clue)
        {
            notice = CaseFile.RemoveClue(clue.Key)
                ? "Crossed out: " + clue.text
                : "That one is worth keeping.";
            Refresh();
        }

        private void BeginTemplate(ClueTemplate template)
        {
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
            notice = added ? "Written down: " + text : "That is already in the book.";
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
