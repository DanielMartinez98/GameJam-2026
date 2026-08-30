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
        //the question the notebook asks while this blank is being filled. Left empty a plain "Which
        //one?" is used, which reads oddly for a time or an object, so a custom slot can name what it is
        //actually asking for - "At what time?", "What did they have on them?".
        public string prompt;
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

    //One step of a confrontation that has to be walked rather than won in a single call. The player is
    //asked for something without being told what it is, and only the right thing moves it on.
    //
    //A suspect with no steps of their own is confronted the plain way, on the one clue in
    //correctClueKey. A suspect with a list of them has to be shown each one in order, which is how the
    //last of them comes to need the whole case rather than one lucky guess.
    [System.Serializable]
    public class ConfrontationStage
    {
        //what has to be put to them here: a notebook clue key, or another suspect's specialClueId
        public string clueKey;
        //What they say while this step is still owed - the ask, made vaguely. Left empty on the first
        //step they simply answer the phone the way they always do.
        [TextArea(2, 4)] public string waiting;
        //what they say once it lands, each section read on its own before the next is offered
        [TextArea(3, 10)] public string[] response;
    }

    //A suspect as the police file and the phone know them. The dining room knows them as a prefab; the
    //two are tied together by prefabName so a clue made about "the Mayor" reaches the same character
    //the player served.
    //
    //On the phone every suspect has four voices, chosen by what the call is: the basic greeting before
    //anything is put to them, the brush off for a clue that means nothing, the moment the one true clue
    //lands, and then them explaining themselves. Putting the true clue to them unlocks a special clue
    //that can then be put to the others, so the suspects break open in a chain.
    [System.Serializable]
    public class SuspectProfile
    {
        //how the player sees them, and what {0} becomes in a filled in clue
        public string displayName = "Suspect";
        //the person behind the handle, shown as the Name on their file. Left empty the handle is used.
        public string realName;
        //must match the suspect prefab's name in the memories, or nothing in the dining room lines up
        public string prefabName;
        public Sprite portrait;
        //what they were doing at the party, which is half of why they were at the table at all
        public string profession;
        [TextArea(3, 6)] public string information;
        [TextArea(3, 6)] public string alibi;
        //what the coroner has to say about this one: the wound that does not match their story, the
        //hand that could not have held the knife. Left empty it is not a line of their file at all.
        [TextArea(3, 6)] public string autopsyReport;
        public string phoneNumber = "555-0000";
        //The detective is on the suspect list because it could have been them, but you cannot call
        //yourself, so the one marked as the player character is left off the phone.
        public bool isPlayerCharacter;

        //Line 1 - the basic call, what they say when picked up before any clue is put to them.
        [TextArea(2, 4)] public string basicLine = "Hello? What is it now, detective?";
        //Line 2 - the brush off, their answer to a clue that means nothing to them.
        [TextArea(2, 4)] public string nonsenseLine = "I have nothing to say about that, detective.";
        //Line 3 - the moment the one true clue lands and they know they are caught.
        [TextArea(2, 4)] public string correctLine = "...how could you possibly know that?";
        //Line 4 - them explaining themselves once the true clue has landed. It can run long, so it is
        //written as sections, each shown as its own paragraph one after the other.
        [TextArea(3, 10)] public string[] explanation;

        //The one clue that breaks this suspect: a notebook clue key, or the special clue id another
        //suspect unlocks, so a special clue turned up on one call can be the thing that breaks the next.
        //Put to them it plays the correct line and the explanation and unlocks this suspect's own
        //special clue. Left empty, nothing breaks them.
        public string correctClueKey;
        //What breaking this suspect turns up, ready to put to the others. The id is the key another
        //suspect's correctClueKey points at; the text is what the player reads on the phone and in the
        //notebook. Leave both empty for a suspect whose call unlocks nothing new.
        public string specialClueId;
        [TextArea(2, 4)] public string specialClue;
        //A confrontation that takes more than one clue. Left empty they break on correctClueKey the way
        //everyone else does; filled in, the steps are walked in the order they are written and
        //correctClueKey, the correct line and the explanation are not read at all.
        public ConfrontationStage[] stages;
        //The murderer. Breaking this one is not another link in the chain, it is the end of the case:
        //their confession is followed by the ending rather than by another number to call. Exactly one
        //suspect should carry it, and a case with none simply never ends on its own.
        public bool endsCase;
    }

    //One finding on a report: what it is called, what it says, and the picture of it. The picture goes
    //in a square frame beside the words rather than above them, which is how a report reads - what was
    //found on the left, what it looked like on the right. A finding with nothing to show is just words.
    [System.Serializable]
    public class CaseFact
    {
        public string label = "Name";
        [TextArea(1, 3)] public string value;
        public Sprite image;
        //What is written over the picture. The photographs are stacked together down the side of the
        //page rather than each one sitting beside its own line, so they are captioned with what they
        //show - "Victim" over the man, not "Name" - and are read without having to be traced back to
        //the finding they came from. Left empty, the finding's own name is used.
        public string imageLabel;
        //A standing figure is far taller than the square frame it goes in, and made to fit whole it
        //comes out as a sliver of a person with a face too small to read. Cropping fills the frame from
        //the top instead, which on a standing figure is the head - the part of a photograph clipped to
        //a report that anyone actually looks at.
        public bool cropImage;
    }

    //One page of the police file. Suspect pages are generated from the profiles above; this covers the
    //autopsy report and anything else that is not a person.
    //
    //A page can be written either way round: as findings, which is a report, or as a body of text with
    //one picture over it, which is a statement. The autopsy is the first kind.
    [System.Serializable]
    public class CasePage
    {
        public string title = "Autopsy report";
        public CaseFact[] facts;
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

        //The cutscene shown on the way into the memory: one picture with a passage of prologue read
        //underneath it. Both are optional - a memory with neither simply drops straight into the dining
        //room the way it always did.
        public Sprite prologueImage;
        [TextArea(3, 8)] public string prologueText;

        //whether there is a cutscene to play at all, so a memory left without one is not stopped on an
        //empty screen on its way in
        public bool HasPrologue
        {
            get { return prologueImage != null || !string.IsNullOrEmpty(prologueText); }
        }
    }
}
