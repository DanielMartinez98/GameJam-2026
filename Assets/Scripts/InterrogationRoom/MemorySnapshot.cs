using System.Collections.Generic;
using UnityEngine;

namespace InterrogationRoom
{
    //A memory walked out of half finished, written down so it can be walked back into exactly as it was
    //left. The dining room is a whole scene that gets torn down and rebuilt from its scene file every
    //time it is entered, so anything that is not in that file has to be carried out by hand - which is
    //everything the player actually did in there.
    //
    //Four things make up "where I was": where the detective was standing, what was still on the board
    //they were carrying, who they had already served, and how much of their order everyone else was
    //still owed. Anything less and coming back would quietly undo some of the work.
    public class MemorySnapshot
    {
        public int memoryIndex = -1;
        //A memory left before the player had even been placed has nothing to say about where they were,
        //and dropping them on the scene's own start point is better than dropping them on the origin.
        public bool hasPlayerPosition;
        public Vector3 playerPosition;
        public readonly List<BoardItem> board = new List<BoardItem>();
        public readonly List<SuspectState> suspects = new List<SuspectState>();

        //One piece of food still on the charcuterie board. The tag is the food's identity everywhere
        //else in the game, so it is what the board is rebuilt from; the position is kept so a board the
        //player arranged at the refill station comes back arranged rather than reshuffled.
        public class BoardItem
        {
            public string foodTag;
            public Vector3 localPosition;
            //A piece of food is rebuilt from its prefab, which knows nothing about how this particular
            //one was laid out in the scene, so anything the scene turned or resized is carried with it.
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        //Matched back to its SuspectSpawnInfo by position in the memory's own array, which is authored
        //data and cannot shift underneath us within a run. The name is carried for readability when
        //something does not line up, not to match on.
        public class SuspectState
        {
            public string suspectName;
            public bool isServed;
            public readonly List<FoodRemaining> foods = new List<FoodRemaining>();
        }

        //How much of one food this suspect is still owed. Matched by id rather than by index so it
        //stays readable next to the order it belongs to.
        public class FoodRemaining
        {
            public string foodItemId;
            public int remaining;
        }
    }
}
