using UnityEngine;

namespace InterrogationRoom
{
    //Where the detective decides which night to go back into. Picking one here always starts it from the
    //beginning, and starting one from the beginning is what throws away a memory left half served - the
    //player is choosing to be somewhere else, and only one memory is ever in progress.
    //
    //Because that choice can cost them their place, it is confirmed rather than taken on the first
    //press, but only when there is actually something to lose.
    public class MemorySelectPanel : RoomPanel
    {
        //the memory waiting on a yes, or -1 when nothing has been picked
        private int pendingIndex = -1;

        protected override string Title
        {
            get { return pendingIndex >= 0 ? "Go back - are you sure?" : "Which memory?"; }
        }

        protected override void OnOpened()
        {
            pendingIndex = -1;
        }

        protected override void Populate()
        {
            if (pendingIndex >= 0)
            {
                PopulateConfirmation();
                return;
            }
            MemoryOption[] options = director != null ? director.Memories : null;
            if (options == null || options.Length == 0)
            {
                AddText("No memories have been set up on the director.", 22f, PanelUI.DimTextColor);
                return;
            }
            foreach (MemoryOption option in options)
            {
                if (option == null)
                {
                    continue;
                }
                MemoryOption chosen = option;
                bool unlocked = CaseFile.IsMemoryUnlocked(option.memoryIndex);
                AddEntry(Label(option), delegate { Choose(chosen); }, unlocked);
                //A locked memory says what would open it rather than only refusing. Its own description
                //is held back until then, since it is a night the detective has not remembered yet.
                if (!unlocked)
                {
                    AddText("  Serve everyone in " + PreviousTitle(options, option.memoryIndex) + " first.",
                        20f, PanelUI.DimTextColor);
                }
                else if (!string.IsNullOrEmpty(option.description))
                {
                    AddText("  " + option.description, 20f, PanelUI.DimTextColor);
                }
            }
        }

        //what the memory before this one is called, for saying which one has to be finished
        private static string PreviousTitle(MemoryOption[] options, int memoryIndex)
        {
            foreach (MemoryOption option in options)
            {
                if (option != null && option.memoryIndex == memoryIndex - 1)
                {
                    return option.title;
                }
            }
            return "the previous memory";
        }

        private string Label(MemoryOption option)
        {
            if (!CaseFile.IsMemoryUnlocked(option.memoryIndex))
            {
                return option.title + "   (locked)";
            }
            //Being part way through one is asked first, because it is the more useful thing to know and
            //the more specific: it is true of exactly one memory, and it stays readable when everything
            //is reading as finished - which is every memory while Unlock All Memories is on.
            if (CaseFile.UnfinishedMemory == option.memoryIndex)
            {
                return option.title + "   (in progress)";
            }
            if (CaseFile.IsMemoryCompleted(option.memoryIndex))
            {
                return option.title + "   (served)";
            }
            return option.title;
        }

        private void PopulateConfirmation()
        {
            AddText("You are part way through another memory.\nGoing back to a memory from the start will lose it.",
                22f, PanelUI.HighlightColor);
            AddEntry("Go back anyway", delegate { Enter(pendingIndex); });
            AddEntry("< Never mind", delegate
            {
                pendingIndex = -1;
                Refresh();
            });
        }

        private void Choose(MemoryOption option)
        {
            //the row is already switched off, so this only catches a locked memory reaching here by
            //some other route
            if (!CaseFile.IsMemoryUnlocked(option.memoryIndex))
            {
                return;
            }
            //returning to the memory already in progress is not losing it, so that one goes straight in
            bool wouldLoseProgress = CaseFile.UnfinishedMemory >= 0 && CaseFile.UnfinishedMemory != option.memoryIndex;
            if (wouldLoseProgress)
            {
                pendingIndex = option.memoryIndex;
                Refresh();
                return;
            }
            Enter(option.memoryIndex);
        }

        private void Enter(int memoryIndex)
        {
            pendingIndex = -1;
            //picked from here, so it starts over: whatever was saved is gone
            director.EnterMemory(memoryIndex, true);
        }
    }
}
