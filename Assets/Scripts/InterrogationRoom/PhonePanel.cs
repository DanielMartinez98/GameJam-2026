using System.Collections.Generic;
using UnityEngine;

namespace InterrogationRoom
{
    //Where suspects get confronted. You call someone first - anyone but yourself - and they pick up with
    //their basic line. From there you can put a clue to them: a clue that means nothing gets the brush
    //off, and the one true clue for that suspect breaks them open, plays their explanation, and unlocks
    //a special clue you can then put to the others. That is how the case comes apart, one call feeding
    //the next.
    //
    //A confrontation can also take more than one clue. Written as steps, the suspect concedes each one
    //and then asks for the next without saying what it is, which is what makes the last of them need
    //the whole case rather than one lucky guess. Both kinds are played the same way from here: a
    //suspect with no steps of their own simply has one.
    //
    //Either way it is read one thing at a time. The card is put back to its top every time it is
    //redrawn, so a confession laid out all at once would have half of it below the fold, unread.
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
        //how far through what they are saying the player has pressed, once a clue has broken them. The
        //call shows one part at a time and this is which one, so it goes back to the start whenever the
        //subject changes.
        private int revealedPart;
        //which step of this suspect's confrontation the call is on. Most of them have only the one.
        private int stage;
        //How far each confrontation has been carried, kept so that hanging up on a man half way through
        //and calling him back picks up where it stopped. It lives here rather than in the case file
        //because it is where a conversation got to, not a fact about the case: walking into a memory
        //and coming back puts everyone on their first line again.
        private readonly Dictionary<string, int> stagesReached = new Dictionary<string, int>();

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

        //A call is read the way it would be heard: one thing said at a time. Nothing is stacked up on
        //the screen, because the list is put back to its top every time it is redrawn - a confession
        //laid out all at once would run off the bottom of the card with its last half unread.
        private void PopulateCall()
        {
            if (!HasBackButton)
            {
                AddEntry(cluePresented ? "< Change the subject" : "< Hang up", Back);
            }

            AddPortrait(calledSuspect.portrait);

            List<ConfrontationStage> steps = Confrontation();
            ConfrontationStage step = stage >= 0 && stage < steps.Count ? steps[stage] : null;

            //Nothing put to them yet: the line they answered with, or, once they have been walked part
            //of the way, whatever they are waiting on - which is them asking for the next thing without
            //naming it.
            if (!cluePresented)
            {
                AddText(Says(Waiting(step)), PanelText.Body);
                PopulateClueOptions();
                return;
            }

            //what you put to them stays above their answer, so a long one is still read against it
            AddText("You: \"" + presentedText + "\"", PanelText.Dim);

            if (!presentedBroke)
            {
                AddText(Says(calledLines.nonsense), PanelText.Body);
                //A confrontation in steps is only as clear as the ask, and the ask is deliberately
                //vague, so it is put back in front of the player rather than lost behind a brush off.
                if (step != null && !string.IsNullOrEmpty(step.waiting))
                {
                    AddText(Says(step.waiting), PanelText.Body);
                }
                PopulateClueOptions();
                return;
            }

            //it landed: what they say about it, a section at a time
            List<string> parts = PartsOf(step);
            if (parts.Count > 0)
            {
                int part = Mathf.Clamp(revealedPart, 0, parts.Count - 1);
                //the first of them is the moment they know they are caught, which is worth catching the
                //eye; everything after it is them talking
                AddText(Says(parts[part]), part == 0 ? PanelText.Note : PanelText.Body);
                if (part < parts.Count - 1)
                {
                    //still talking, so there is nothing to do but hear the rest of it
                    AddEntry("> Go on", GoOn);
                    return;
                }
            }

            //Done with this step but not with them: they go back to waiting, one step further along,
            //and the next thing they want is the next thing the player has to go and find.
            if (stage < steps.Count - 1)
            {
                AddEntry("> Go on", NextStep);
                return;
            }

            //they have finished: what the call turned up, and what there is left to do about it
            if (!string.IsNullOrEmpty(calledLines.specialClue))
            {
                AddText("\nNew clue: " + calledLines.specialClue, PanelText.Note);
            }
            //The murderer. There is no next number to call and nothing left to bring up, so the call
            //ends on the one thing left to do rather than on the list of clues.
            if (calledLines.endsCase)
            {
                AddText("\nThat is the case.", PanelText.Dim);
                AddEntry("> Close the case", CloseTheCase);
                return;
            }

            PopulateClueOptions();
        }

        //one side of the call, written the way the other side is
        private string Says(string line)
        {
            return calledSuspect.displayName + ": \"" + line + "\"";
        }

        //The confrontation as a list of steps, however it happens to have been written. A suspect with
        //steps of their own is walked through them in order; everyone else has exactly one - the clue
        //that breaks them and everything they say when it lands - so both play the same way from here.
        private List<ConfrontationStage> Confrontation()
        {
            List<ConfrontationStage> steps = new List<ConfrontationStage>();
            if (calledLines.stages != null)
            {
                foreach (ConfrontationStage authored in calledLines.stages)
                {
                    //a step with nothing to ask for could never be got past
                    if (authored != null && !string.IsNullOrEmpty(authored.clueKey))
                    {
                        steps.Add(authored);
                    }
                }
            }
            if (steps.Count == 0)
            {
                steps.Add(new ConfrontationStage
                {
                    clueKey = calledLines.correctClueKey,
                    response = BrokenParts()
                });
            }
            return steps;
        }

        //what they say while a step is still owed, falling back to the line they answered the phone with
        private string Waiting(ConfrontationStage step)
        {
            return step != null && !string.IsNullOrEmpty(step.waiting) ? step.waiting : calledLines.basic;
        }

        //what a step has them say, with anything left blank dropped rather than shown as an empty press
        private static List<string> PartsOf(ConfrontationStage step)
        {
            List<string> parts = new List<string>();
            if (step != null && step.response != null)
            {
                foreach (string section in step.response)
                {
                    if (!string.IsNullOrEmpty(section))
                    {
                        parts.Add(section);
                    }
                }
            }
            return parts;
        }

        //Everything a plainly written suspect says once they are broken, in the order they say it: the
        //moment the clue lands, then each section of their explanation. This is the one step a
        //confrontation without steps of its own is built from.
        private string[] BrokenParts()
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(calledLines.correct))
            {
                parts.Add(calledLines.correct);
            }
            if (calledLines.explanation != null)
            {
                parts.AddRange(calledLines.explanation);
            }
            return parts.ToArray();
        }

        //let them say the next part of it
        private void GoOn()
        {
            revealedPart++;
            Refresh();
        }

        //Done with this step: they stop talking and go back to waiting for the next thing. How far each
        //confrontation has been carried is remembered, so hanging up on a man half way through and
        //calling him back does not put him at the beginning again.
        private void NextStep()
        {
            stage++;
            if (calledSuspect != null && !string.IsNullOrEmpty(calledSuspect.displayName))
            {
                stagesReached[calledSuspect.displayName] = stage;
            }
            ClearPresented();
            Refresh();
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
            //picked up where this one was left, which for all but a staged confrontation is the start
            int reached;
            stage = suspect != null && suspect.displayName != null
                && stagesReached.TryGetValue(suspect.displayName, out reached) ? reached : 0;
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
            //a new subject starts them talking from the beginning, whatever was said about the last one
            revealedPart = 0;
            //only the thing they are waiting for lands. A confrontation in steps will not take the last
            //of them early: put out of order, a clue is just another thing they have nothing to say to.
            List<ConfrontationStage> steps = Confrontation();
            ConfrontationStage step = stage >= 0 && stage < steps.Count ? steps[stage] : null;
            presentedBroke = step != null && !string.IsNullOrEmpty(step.clueKey)
                && clue.key == step.clueKey;
            if (presentedBroke)
            {
                //a written clue that broke someone is struck off in the notebook; a special clue has no
                //notebook row to mark, so FindClue simply finds nothing and nothing is struck
                CaseFile.Clue written = CaseFile.FindClue(clue.key);
                if (written != null)
                {
                    CaseFile.MarkClueUsed(written);
                }
                //The call turned something up: it goes to the special clues so it survives the phone
                //going down and can be put to the other suspects. Only the last step turns anything up,
                //since a confrontation half walked has not finished telling you anything yet.
                if (stage >= steps.Count - 1)
                {
                    CaseFile.AddSpecialClue(calledLines.specialClueId, calledLines.specialClue);
                }
            }
            Refresh();
        }

        //The confession has been read and the player is done with it: the phone goes down for the last
        //time and the room hands the case over to its ending.
        private void CloseTheCase()
        {
            if (director != null)
            {
                director.ShowEnding();
            }
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
            revealedPart = 0;
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
