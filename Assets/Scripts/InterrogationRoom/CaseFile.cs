using System.Collections.Generic;
using UnityEngine;

namespace InterrogationRoom
{
    //The one thing in the game that has to survive a scene load. The interrogation room and the dining
    //room are two scenes, and walking out of a memory half served has to still be true when the room
    //comes back, so the memory in progress and the clues written down live here rather than on any
    //object in either scene. Static on purpose: nothing has to be wired up, nothing has to be carried
    //between scenes by hand, and there is exactly one case being worked.
    public static class CaseFile
    {
        //A clue the player has actually written down: which template it came from, what they filled it
        //in with, and the sentence that produced. The key is what the suspects' responses are matched
        //on, so two clues off the same template with different picks are different accusations.
        public class Clue
        {
            public string templateId;
            public string[] picks;
            public string text;
            //set once a suspect has answered this clue with a breakthrough, so the notebook can show
            //which accusations are already spent
            public bool used;

            public string Key
            {
                get { return BuildKey(templateId, picks); }
            }

            //Whether this is one of the true clues. Asked of the answers file every time rather than
            //stored on the clue, so editing the answers takes effect on a save that already exists
            //instead of leaving old clues judged by an old version of the solution.
            public bool IsCorrect
            {
                get { return CorrectClues.IsCorrect(Key); }
            }
        }

        //"allergy|Mayor|Grapes" - the template first, then the picks in slot order
        public static string BuildKey(string templateId, string[] picks)
        {
            string key = templateId == null ? string.Empty : templateId;
            if (picks != null)
            {
                foreach (string pick in picks)
                {
                    key += "|" + pick;
                }
            }
            return key;
        }

        private static readonly List<Clue> clues = new List<Clue>();
        private static readonly HashSet<int> completedMemories = new HashSet<int>();

        //The memory the player walked out of without serving everyone. -1 means there is none, which is
        //what greys out the Memory button in the interrogation room.
        public static int UnfinishedMemory { get; private set; } = -1;

        //What the dining room should start when it loads. It is read once and cleared, so re-entering
        //the dining room some other way cannot silently restart a memory the player did not pick.
        private static int pendingMemory = -1;

        public static IList<Clue> Clues
        {
            get { Load(); return clues; }
        }

        //Writing the same accusation twice would put two identical lines in the notebook and give the
        //phone two entries that do the same thing, so a clue already written is quietly kept.
        public static bool AddClue(string templateId, string[] picks, string text)
        {
            Load();
            string key = BuildKey(templateId, picks);
            foreach (Clue existing in clues)
            {
                if (existing.Key == key)
                {
                    return false;
                }
            }
            clues.Add(new Clue { templateId = templateId, picks = picks, text = text });
            Save();
            return true;
        }

        //Throwing away a wrong guess is the player tidying up. Throwing away a right one would be
        //throwing away progress they have already made and might not be able to make again, so a clue
        //that is part of the solution is refused rather than removed.
        public static bool RemoveClue(string key)
        {
            Load();
            for (int i = 0; i < clues.Count; i++)
            {
                if (clues[i].Key != key)
                {
                    continue;
                }
                if (clues[i].IsCorrect)
                {
                    return false;
                }
                clues.RemoveAt(i);
                Save();
                return true;
            }
            return false;
        }

        public static Clue FindClue(string key)
        {
            Load();
            foreach (Clue clue in clues)
            {
                if (clue.Key == key)
                {
                    return clue;
                }
            }
            return null;
        }

        //a suspect broke on this accusation, which is worth keeping across a quit
        public static void MarkClueUsed(Clue clue)
        {
            if (clue == null || clue.used)
            {
                return;
            }
            clue.used = true;
            Save();
        }

        //Asked for by the interrogation room when a memory is picked, read by the dining room when it
        //loads. Picking a new memory is also what throws away the half finished one: the player is
        //choosing to start over somewhere else, and the game only ever holds one memory in progress.
        public static void RequestMemory(int memoryIndex, bool clearUnfinished)
        {
            pendingMemory = memoryIndex;
            if (clearUnfinished)
            {
                UnfinishedMemory = -1;
                //Starting over means starting over: the board, the position and the half filled orders
                //go with the memory they belonged to. Leaving the snapshot behind would put the player
                //back where they left off in a memory they asked to begin again.
                snapshot = null;
            }
        }

        //where the player was when they walked out of the memory in progress, or null if there is none
        private static MemorySnapshot snapshot;

        public static void SaveSnapshot(MemorySnapshot state)
        {
            snapshot = state;
        }

        //Only ever hands back a snapshot of the memory being asked for. A snapshot belongs to one
        //memory, and restoring one memory's board and positions into another would be nonsense.
        public static MemorySnapshot GetSnapshot(int memoryIndex)
        {
            return snapshot != null && snapshot.memoryIndex == memoryIndex ? snapshot : null;
        }

        public static void ClearSnapshot()
        {
            snapshot = null;
        }

        //Read once. A second caller gets -1, so nothing restarts a memory behind the player's back.
        public static int TakeRequestedMemory()
        {
            int memoryIndex = pendingMemory;
            pendingMemory = -1;
            return memoryIndex;
        }

        //The dining room says how a memory ended. Everyone served means it is done and there is nothing
        //to come back to; anything less is left on the Memory button to be picked up again.
        public static void ReportMemoryLeft(int memoryIndex, bool completed)
        {
            if (memoryIndex < 0)
            {
                return;
            }
            Load();
            if (completed)
            {
                completedMemories.Add(memoryIndex);
                //finishing a memory is what opens the next one, so it is worth a quit
                Save();
                if (UnfinishedMemory == memoryIndex)
                {
                    UnfinishedMemory = -1;
                }
                //everyone was served, so there is no half done state left worth keeping
                snapshot = null;
            }
            else
            {
                UnfinishedMemory = memoryIndex;
            }
        }

        //While the game is being built there is no patience for playing memory one through to unlock
        //memory two every time something in memory three needs looking at. With this on, every memory
        //answers as finished and so every memory is open.
        //
        //Deliberately not written to the save: it is an answer given while the question is being asked,
        //not a fact recorded about the case. Switching it off hands back the real progress underneath,
        //with nothing to undo and no save to repair.
        public static bool UnlockEverything;

        public static bool IsMemoryCompleted(int memoryIndex)
        {
            if (UnlockEverything)
            {
                return true;
            }
            Load();
            return completedMemories.Contains(memoryIndex);
        }

        //The memories are worked in order: the first is always open, and each one after it is earned by
        //serving everyone in the one before. Nothing else gates them, so a player who has finished the
        //second memory can still go back into the first.
        public static bool IsMemoryUnlocked(int memoryIndex)
        {
            if (memoryIndex <= 0)
            {
                return true;
            }
            return IsMemoryCompleted(memoryIndex - 1);
        }

        //True once every memory can be entered, which is the state the whole case can be accused in.
        //Note this is one short of having played them all: the last memory is unlocked by finishing the
        //one before it, not by being finished itself. AllMemoriesCompleted is the stricter question.
        public static bool AllMemoriesUnlocked(int memoryCount)
        {
            for (int i = 0; i < memoryCount; i++)
            {
                if (!IsMemoryUnlocked(i))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool AllMemoriesCompleted(int memoryCount)
        {
            for (int i = 0; i < memoryCount; i++)
            {
                if (!IsMemoryCompleted(i))
                {
                    return false;
                }
            }
            return true;
        }

        //Statics outlive a scene load, which is the point of them, but they also outlive the run itself
        //when the editor is set to skip its domain reload. A new game starts from here, and it clears
        //what is on disk too - otherwise the next read would put the old case straight back.
        public static void Reset()
        {
            loaded = true;
            clues.Clear();
            completedMemories.Clear();
            UnfinishedMemory = -1;
            pendingMemory = -1;
            snapshot = null;
            Save();
        }

        //What survives quitting the game: the clues written down and the memories finished. The memory
        //in progress deliberately does not - it is where the player was standing and what was on their
        //board, which is a moment in a scene rather than a fact about the case, and it belongs to the
        //session that was interrupted.
        [System.Serializable]
        private class SavedState
        {
            public List<SavedClue> clues = new List<SavedClue>();
            public List<int> completedMemories = new List<int>();
        }

        [System.Serializable]
        private class SavedClue
        {
            public string templateId;
            public string[] picks;
            public string text;
            public bool used;
        }

        private static bool loaded;

        private static string SavePath
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "case-file.json"); }
        }

        //Every read goes through here, so nothing can look at an empty case just because nobody thought
        //to load it first. Cheap after the first call.
        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            //set before reading, so a failure part way through cannot send this round again
            loaded = true;
            string path = SavePath;
            if (!System.IO.File.Exists(path))
            {
                return;
            }
            SavedState state = null;
            try
            {
                state = JsonUtility.FromJson<SavedState>(System.IO.File.ReadAllText(path));
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("The saved case file could not be read, starting a fresh one: " + error.Message);
            }
            if (state == null)
            {
                return;
            }
            clues.Clear();
            if (state.clues != null)
            {
                foreach (SavedClue saved in state.clues)
                {
                    if (saved != null)
                    {
                        clues.Add(new Clue
                        {
                            templateId = saved.templateId,
                            picks = saved.picks,
                            text = saved.text,
                            used = saved.used
                        });
                    }
                }
            }
            completedMemories.Clear();
            if (state.completedMemories != null)
            {
                foreach (int memoryIndex in state.completedMemories)
                {
                    completedMemories.Add(memoryIndex);
                }
            }
        }

        //Written out whenever any of it changes. The case is a handful of short strings, so there is
        //nothing to be gained by batching it up and something to lose if the game is closed in between.
        private static void Save()
        {
            SavedState state = new SavedState();
            foreach (Clue clue in clues)
            {
                state.clues.Add(new SavedClue
                {
                    templateId = clue.templateId,
                    picks = clue.picks,
                    text = clue.text,
                    used = clue.used
                });
            }
            foreach (int memoryIndex in completedMemories)
            {
                state.completedMemories.Add(memoryIndex);
            }
            try
            {
                System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(state, true));
            }
            catch (System.Exception error)
            {
                Debug.LogError("The case file could not be saved: " + error.Message);
            }
        }

        //where the save actually lives, for anyone who needs to go and look at it
        public static string SaveFilePath
        {
            get { return SavePath; }
        }
    }
}
