using System.Collections.Generic;
using UnityEngine;

namespace InterrogationRoom
{
    //Where suspects get confronted. A call is never made empty handed: the clue is picked first and the
    //suspect second, so what the player is doing is always putting one specific accusation to one
    //specific person, and the answer can be written for exactly that pairing.
    public class PhonePanel : RoomPanel
    {
        //the accusation being made, or null while the player is still choosing which one to make
        private CaseFile.Clue selectedClue;
        //who was called and what they said back, held until something else is pressed
        private SuspectProfile calledSuspect;
        private string answer;

        protected override string Title
        {
            get
            {
                if (calledSuspect != null)
                {
                    return "Phone - " + calledSuspect.displayName;
                }
                if (selectedClue != null)
                {
                    return "Phone - who do you call?";
                }
                return "Phone - pick a clue";
            }
        }

        protected override void OnOpened()
        {
            //the phone is put down between openings, so it is not still holding last call's line
            selectedClue = null;
            calledSuspect = null;
            answer = null;
        }

        protected override void Populate()
        {
            if (calledSuspect != null)
            {
                PopulateAnswer();
            }
            else if (selectedClue == null)
            {
                PopulateClueList();
            }
            else
            {
                PopulateSuspectList();
            }
        }

        private void PopulateClueList()
        {
            IList<CaseFile.Clue> clues = CaseFile.Clues;
            if (clues.Count == 0)
            {
                AddText("You have nothing to say to anyone yet.\nOpen the notebook and write a clue first.",
                    22f, PanelUI.DimTextColor);
                return;
            }
            AddText("What are you calling about?", 22f, PanelUI.DimTextColor);
            foreach (CaseFile.Clue clue in clues)
            {
                CaseFile.Clue chosen = clue;
                //a spent clue can still be read out, it just will not land twice, so it is listed
                //greyed rather than hidden
                string label = clue.used ? clue.text + "   (already used)" : clue.text;
                AddEntry(label, delegate { SelectClue(chosen); }).interactable = !clue.used;
            }
        }

        private void PopulateSuspectList()
        {
            AddEntry("< Back", Back);
            AddText("\"" + selectedClue.text + "\"", 22f, PanelUI.HighlightColor);
            SuspectProfile[] suspects = director != null ? director.Suspects : null;
            if (suspects == null || suspects.Length == 0)
            {
                AddText("There are no numbers in the book.", 22f, PanelUI.DimTextColor);
                return;
            }
            foreach (SuspectProfile suspect in suspects)
            {
                if (suspect == null)
                {
                    continue;
                }
                SuspectProfile chosen = suspect;
                AddEntry(suspect.displayName + "   " + suspect.phoneNumber, delegate { Call(chosen); });
            }
        }

        private void PopulateAnswer()
        {
            AddEntry("< Hang up", Back);
            AddText("You: \"" + selectedClue.text + "\"", 22f, PanelUI.DimTextColor);
            AddText(calledSuspect.displayName + ": \"" + answer + "\"", 24f, PanelUI.TextColor);
        }

        private void SelectClue(CaseFile.Clue clue)
        {
            selectedClue = clue;
            Refresh();
        }

        //One step back at a time: hanging up returns to the list of people to call, and backing out of
        //that returns to the clues, so a wrong pick never costs the player the whole call.
        private void Back()
        {
            if (calledSuspect != null)
            {
                calledSuspect = null;
                answer = null;
            }
            else
            {
                selectedClue = null;
            }
            Refresh();
        }

        private void Call(SuspectProfile suspect)
        {
            calledSuspect = suspect;
            ClueResponse response = FindResponse(suspect, selectedClue.Key);
            answer = response != null && !string.IsNullOrEmpty(response.line) ? response.line : suspect.brushOffLine;
            //A clue that actually broke someone has done its work, and the phone stops offering it. A
            //clue that got a brush off is left alone: the same accusation may well land on someone else.
            if (response != null && response.isBreakthrough)
            {
                //through the case file rather than set here, so the breakthrough is written to disk
                //along with it and is still spent after a quit
                CaseFile.MarkClueUsed(selectedClue);
            }
            Refresh();
        }

        private static ClueResponse FindResponse(SuspectProfile suspect, string clueKey)
        {
            if (suspect.responses == null)
            {
                return null;
            }
            foreach (ClueResponse response in suspect.responses)
            {
                if (response != null && response.clueKey == clueKey)
                {
                    return response;
                }
            }
            return null;
        }
    }
}
