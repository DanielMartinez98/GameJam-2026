using System.Collections.Generic;
using UnityEngine;

namespace InterrogationRoom
{
    //Where suspects get confronted. You call someone first - anyone but yourself - and they pick up with
    //their basic line. From there you can put a clue to them: a clue that means nothing gets the brush
    //off, and the one true clue for that suspect breaks them open, plays their explanation, and unlocks
    //a special clue you can then put to the others. That is how the case comes apart, one call feeding
    //the next.
    public class PhonePanel : RoomPanel
    {
        [Header("Phone")]
        //the suspect's picture, shown at the top of the call. The same captioned photograph the case
        //file uses, so a face looks the same in both places; left empty, a call simply shows no portrait.
        [SerializeField] private CasePlate portraitPrefab;

        //One thing the player can put to a suspect: a clue written in the notebook, or a special clue
        //an earlier call turned up. Both are matched to a suspect the same way, by their key.
        private struct Presentable
        {
            public string key;
            public string text;
            public bool isSpecial;
        }

        //who is on the line, or null while the player is still deciding who to call
        private SuspectProfile calledSuspect;
        //their lines with the centralized phone script folded over the scene, resolved once on the call
        private PhoneScript.Lines calledLines;
        //the clue currently put to them, held so the answer stays on screen while the options to bring
        //up something else sit under it. Cleared when the subject is changed or the phone goes down.
        private bool cluePresented;
        private string presentedText;
        private bool presentedBroke;

        protected override string Title
        {
            get
            {
                if (calledSuspect != null)
                {
                    return "Phone - " + calledSuspect.displayName;
                }
                return "Phone - who do you call?";
            }
        }

        protected override void OnOpened()
        {
            //the phone is put down between openings, so it is not still holding last call's line
            calledSuspect = null;
            ClearPresented();
        }

        protected override void Populate()
        {
            if (calledSuspect == null)
            {
                PopulateSuspectList();
            }
            else
            {
                PopulateCall();
            }
        }

        //One step back at a time: while a clue is on the line, backing out drops the clue but keeps the
        //call; on the bare call it hangs up; on the list of people there is nowhere left to go.
        protected override bool CanGoBack
        {
            get { return calledSuspect != null; }
        }

        protected override void GoBack()
        {
            Back();
        }

        private void PopulateSuspectList()
        {
            SuspectProfile[] suspects = director != null ? director.Suspects : null;
            if (suspects == null || suspects.Length == 0)
            {
                AddText("There are no numbers in the book.", PanelText.Dim);
                return;
            }
            AddText("Who do you call?", PanelText.Dim);
            foreach (SuspectProfile suspect in suspects)
            {
                //you cannot call yourself: the detective is on the suspect list for the file, not the phone
                if (suspect == null || suspect.isPlayerCharacter)
                {
                    continue;
                }
                SuspectProfile chosen = suspect;
                //a suspect already broken open is marked, so the player can see who is left to work on
                string label = suspect.displayName + "   " + suspect.phoneNumber;
                if (IsBroken(suspect))
                {
                    label += "   (spoken to)";
                }
                AddEntry(label, delegate { Call(chosen); });
            }
        }

        private void PopulateCall()
        {
            if (!HasBackButton)
            {
                AddEntry(cluePresented ? "< Change the subject" : "< Hang up", Back);
            }

            AddPortrait(calledSuspect.portrait);

            //they always pick up with their basic line before anything is put to them
            AddText(calledSuspect.displayName + ": \"" + calledLines.basic + "\"", PanelText.Body);

            if (cluePresented)
            {
                AddText("\nYou: \"" + presentedText + "\"", PanelText.Dim);
                if (presentedBroke)
                {
                    AddText(calledSuspect.displayName + ": \"" + calledLines.correct + "\"", PanelText.Note);
                    //line 4 - them explaining themselves, each authored section its own paragraph
                    if (calledLines.explanation != null)
                    {
                        foreach (string section in calledLines.explanation)
                        {
                            if (!string.IsNullOrEmpty(section))
                            {
                                AddText(section, PanelText.Body);
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(calledLines.specialClue))
                    {
                        AddText("\nNew clue: " + calledLines.specialClue, PanelText.Note);
                    }
                }
                else
                {
                    AddText(calledSuspect.displayName + ": \"" + calledLines.nonsense + "\"", PanelText.Body);
                }
            }

            PopulateClueOptions();
        }

        //The suspect's picture at the top of the call, shown the same way as in the case file: filled
        //into its frame and cropped from the top, which on a standing figure is the head.
        private void AddPortrait(Sprite portrait)
        {
            if (portrait == null)
            {
                return;
            }
            if (portraitPrefab == null || itemsParent == null)
            {
                Missing("Portrait Prefab");
                return;
            }
            Instantiate(portraitPrefab, itemsParent).Set(string.Empty, portrait, true);
        }

        private void PopulateClueOptions()
        {
            List<Presentable> options = GatherClues();
            if (options.Count == 0)
            {
                AddText("\nYou have nothing to bring up yet.\nOpen the notebook and write a clue first.",
                    PanelText.Dim);
                return;
            }
            AddText("\nBring something up:", PanelText.Dim);
            foreach (Presentable option in options)
            {
                Presentable chosen = option;
                //a special clue is marked so the player knows it came off the phone, not their notebook
                string label = option.isSpecial ? option.text + "   (special)" : option.text;
                AddEntry(label, delegate { Present(chosen); });
            }
        }

        //Everything the player could put to a suspect: the clues they have written down, then the
        //special clues the phone has turned up, each keyed so it can be matched to whoever it breaks.
        private static List<Presentable> GatherClues()
        {
            List<Presentable> options = new List<Presentable>();
            foreach (CaseFile.Clue clue in CaseFile.Clues)
            {
                options.Add(new Presentable { key = clue.Key, text = clue.text, isSpecial = false });
            }
            foreach (CaseFile.SpecialClue special in CaseFile.SpecialClues)
            {
                options.Add(new Presentable { key = special.id, text = special.text, isSpecial = true });
            }
            return options;
        }

        private void Call(SuspectProfile suspect)
        {
            calledSuspect = suspect;
            calledLines = PhoneScript.Resolve(suspect);
            ClearPresented();
            Refresh();
        }

        //Puts one clue to whoever is on the line. The one true clue for this suspect breaks them open:
        //it spends the written clue, plays the explanation, and drops their special clue where the other
        //suspects can be confronted with it. Anything else gets the brush off and costs nothing.
        private void Present(Presentable clue)
        {
            cluePresented = true;
            presentedText = clue.text;
            presentedBroke = !string.IsNullOrEmpty(calledLines.correctClueKey)
                && clue.key == calledLines.correctClueKey;
            if (presentedBroke)
            {
                //a written clue that broke someone is struck off in the notebook; a special clue has no
                //notebook row to mark, so FindClue simply finds nothing and nothing is struck
                CaseFile.Clue written = CaseFile.FindClue(clue.key);
                if (written != null)
                {
                    CaseFile.MarkClueUsed(written);
                }
                //the call turned something up: it goes to the special clues so it survives the phone
                //going down and can be put to the other suspects
                CaseFile.AddSpecialClue(calledLines.specialClueId, calledLines.specialClue);
            }
            Refresh();
        }

        //One step back at a time: dropping the clue keeps the call, hanging up returns to the list.
        private void Back()
        {
            if (cluePresented)
            {
                ClearPresented();
            }
            else
            {
                calledSuspect = null;
            }
            Refresh();
        }

        private void ClearPresented()
        {
            cluePresented = false;
            presentedText = null;
            presentedBroke = false;
        }

        //A suspect is broken once the special clue they unlock has been turned up. A suspect who unlocks
        //nothing can never read as broken, which is fine - there is no chain hanging off them. The id is
        //resolved through the script so a special clue moved to the document still marks them.
        private static bool IsBroken(SuspectProfile suspect)
        {
            return suspect != null && CaseFile.HasSpecialClue(PhoneScript.Resolve(suspect).specialClueId);
        }
    }
}
