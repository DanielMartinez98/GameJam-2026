using UnityEngine;

//Everything the interrogation room reads but never writes: the suspects, the clue templates they can be
//confronted with, the pages of the police file and the three memories that can be walked back into.
//It is all authored in the scene on the InterrogationRoomDirector, the same way the memories themselves
//are authored on the GameDirectorMemories, so the game only ever reads constants out of it.
namespace InterrogationRoom
{
    //What a template slot asks the player to pick. Suspect and Food are filled from the director's own
    //lists, so a template saying "{0} is allergic to {1}" needs nothing authored on the slot itself.
    //Custom is for anything else - times, rooms, objects - and carries its own options.
    public enum ClueSlotKind
    {
        Suspect,
        Food,
        Custom
    }

    [System.Serializable]
    public class ClueSlot
    {
        public ClueSlotKind kind = ClueSlotKind.Suspect;
        //only read when kind is Custom. Each entry is both what the player sees and what the suspect
        //responses are matched against, so keep them short and stable once responses are written.
        public string[] customOptions;
    }

    //One of the premade sentences in the notebook. The player picks the template first and then fills it
    //in slot by slot, so "{0} is allergic to {1}" becomes "the Mayor is allergic to Grapes" only once
    //both picks are made.
    [System.Serializable]
    public class ClueTemplate
    {
        //short and stable: it is half of the key the suspects' responses are matched on, so renaming it
        //silently unhooks every response written against it
        public string templateId;
        //what the notebook lists this template as before any slot is filled
        public string menuLabel = "New clue";
        //{0}, {1}, ... are replaced by the picks, in slot order
        [TextArea(2, 3)] public string sentence = "{0} is allergic to {1}";
        public ClueSlot[] slots;
    }

    //A suspect as the police file and the phone know them. The dining room knows them as a prefab; the
    //two are tied together by prefabName so a clue made about "the Mayor" reaches the same character
    //the player served.
    [System.Serializable]
    public class SuspectProfile
    {
        //how the player sees them, and what {0} becomes in a filled in clue
        public string displayName = "Suspect";
        //must match the suspect prefab's name in the memories, or nothing in the dining room lines up
        public string prefabName;
        public Sprite portrait;
        [TextArea(3, 6)] public string information;
        [TextArea(3, 6)] public string alibi;
        public string phoneNumber = "555-0000";
        //what they say when called with a clue that means nothing to them
        [TextArea(2, 4)] public string brushOffLine = "I have nothing to say about that, detective.";
        public ClueResponse[] responses;
    }

    //One suspect's answer to one fully filled in clue. The clue is identified by its key - the template
    //id and the picks joined by "|", exactly as CaseFile.Clue builds it - so a response is written
    //against a specific accusation rather than against the template in general.
    [System.Serializable]
    public class ClueResponse
    {
        //e.g. "allergy|Mayor|Grapes". The director's context menu prints the keys every clue in the
        //scene can produce, so these never have to be guessed at.
        public string clueKey;
        [TextArea(2, 5)] public string line;
        //a response that actually moves the case forward, so the notebook can mark the clue spent
        public bool isBreakthrough;
    }

    //One page of the police file. Suspect pages are generated from the profiles above; this covers the
    //autopsy report and anything else that is not a person.
    [System.Serializable]
    public class CasePage
    {
        public string title = "Autopsy report";
        [TextArea(6, 20)] public string body;
        public Sprite image;
    }

    //One of the three memories, as the player picks it rather than as the dining room spawns it. The
    //index is the one GameDirectorMemories.StartMemory takes.
    [System.Serializable]
    public class MemoryOption
    {
        public string title = "Memory";
        [TextArea(2, 4)] public string description;
        public Sprite thumbnail;
        public int memoryIndex;
    }
}
