using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

//How a spoken line is put on screen and how long it stays there. Both screens talk to the player the
//same way, so the rules live here rather than twice over.
//A line types itself out at a fixed rate, which means a long one takes longer to arrive than a short
//one, and then sits whole for a beat so it can actually be read. That beat is the only fixed part: the
//typing is what scales with the line, and the wait the player is being asked for is the two together.
public static class ConversationLine
{
    //roughly reading pace - fast enough not to be a chore, slow enough to register as being spoken
    public const float DefaultCharactersPerSecond = 28f;
    //the beat a finished line is left up for, on top of however long it took to type
    public const float DefaultPause = 2f;
    private static readonly Color DefaultHighlight = new Color(1f, 0.85f, 0.4f, 1f);
    //a lift of a few pixels, a few cycles a second, and enough phase between letters that the crest
    //travels along the word instead of the whole thing bobbing at once
    public const float DefaultWaveAmplitude = 4f;
    public const float DefaultWaveFrequency = 6f;
    public const float DefaultWaveStep = 0.55f;

    private static float Rate(float charactersPerSecond)
    {
        return charactersPerSecond > 0f ? charactersPerSecond : DefaultCharactersPerSecond;
    }

    //Counts what will actually be typed rather than what was authored. Markup is not read out loud, so
    //a line picked out in colour must not take any longer to arrive than the same words left plain.
    public static int VisibleLength(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return 0;
        }
        int count = 0;
        bool insideTag = false;
        foreach (char character in line)
        {
            if (character == '<')
            {
                insideTag = true;
            }
            else if (character == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                count++;
            }
        }
        return count;
    }

    //how long the line takes to finish typing itself out
    public static float RevealSeconds(string line, float charactersPerSecond)
    {
        return VisibleLength(line) / Rate(charactersPerSecond);
    }

    //The whole wait: typing plus the beat afterwards. This is what the countdown ring is measured
    //against, so the ring empties exactly as the line becomes readable and then runs out the pause.
    public static float DurationFor(string line, float charactersPerSecond, float pause)
    {
        return RevealSeconds(line, charactersPerSecond) + (pause > 0f ? pause : DefaultPause);
    }

    //Words to pick out are authored as *asterisks* around them - one character either side, and no hex
    //codes buried in the dialogue. They become colour tags here, in whichever colour the screen is set
    //to highlight with, so that colour is changed in one place instead of in every line that uses it.
    //Anything written out as a real tag is passed through untouched, so the whole of TMP's rich text is
    //still available for anything this does not cover.
    //Ranges is filled with where the highlighted words land once the markup is gone - counted in the
    //characters the player actually sees, which is the same numbering the mesh uses, so the animation
    //can find those letters again without having to guess at them from their colour.
    public static string Highlight(string line, Color highlight, List<Vector2Int> ranges)
    {
        if (ranges != null)
        {
            ranges.Clear();
        }
        if (string.IsNullOrEmpty(line) || line.IndexOf('*') < 0)
        {
            return line;
        }
        bool unset = highlight.r == 0f && highlight.g == 0f && highlight.b == 0f && highlight.a == 0f;
        string opening = "<color=#" + ColorUtility.ToHtmlStringRGB(unset ? DefaultHighlight : highlight) + ">";
        StringBuilder built = new StringBuilder(line.Length + 32);
        bool colouring = false;
        bool insideTag = false;
        int visible = 0;
        int wordStart = 0;
        foreach (char character in line)
        {
            if (character == '*')
            {
                if (colouring)
                {
                    built.Append("</color>");
                    if (ranges != null)
                    {
                        ranges.Add(new Vector2Int(wordStart, visible - wordStart));
                    }
                }
                else
                {
                    built.Append(opening);
                    wordStart = visible;
                }
                colouring = !colouring;
                continue;
            }
            built.Append(character);
            //tags already written into the line are passed through but are not characters on screen
            if (character == '<')
            {
                insideTag = true;
            }
            else if (character == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                visible++;
            }
        }
        //an asterisk left unclosed would otherwise colour everything after it, and the next line too
        if (colouring)
        {
            built.Append("</color>");
            if (ranges != null)
            {
                ranges.Add(new Vector2Int(wordStart, visible - wordStart));
            }
        }
        return built.ToString();
    }

    //Puts a line up with none of it showing yet, ready for Reveal to walk along it, and hands back how
    //long it should stay - worked out from the marked up line, so the two can never disagree.
    public static float Begin(TextMeshProUGUI text, string line, Color highlight,
                              float charactersPerSecond, float pause, List<Vector2Int> ranges)
    {
        string shown = Highlight(line, highlight, ranges);
        if (text != null)
        {
            text.text = shown;
            text.maxVisibleCharacters = 0;
            //laid out now so the character count is right on the first frame rather than the second
            text.ForceMeshUpdate();
        }
        return DurationFor(shown, charactersPerSecond, pause);
    }

    //Typed out by drawing fewer of the glyphs already laid out, rather than by rebuilding the string
    //every frame: TMP lays the line out once and maxVisibleCharacters just moves along it. Costs no
    //allocations, keeps the wrapping identical from the first character to the last so the panel does
    //not reflow as the words arrive, and counts parsed characters - so a colour tag is not something
    //the player has to sit through being typed.
    public static void Reveal(TextMeshProUGUI text, float elapsed, float charactersPerSecond)
    {
        if (text == null)
        {
            return;
        }
        int total = text.textInfo != null && text.textInfo.characterCount > 0
            ? text.textInfo.characterCount
            : VisibleLength(text.text);
        int shown = Mathf.Clamp(Mathf.FloorToInt(elapsed * Rate(charactersPerSecond)), 0, total);
        text.maxVisibleCharacters = shown;
    }

    //Lifts the highlighted letters on a sine, stepping the phase along the word so the crest travels
    //through it rather than the whole word bobbing at once.
    //TMP hands out the laid out mesh, so this moves the four corners of each letter's quad and puts the
    //geometry back. The layout is rebuilt first every frame: the offsets are written into the same
    //vertex array each time, so without that they would pile up and the word would climb off the panel.
    public static void Wave(TextMeshProUGUI text, List<Vector2Int> ranges, float time,
                            float amplitude, float frequency, float perCharacterStep)
    {
        if (text == null || ranges == null || ranges.Count == 0)
        {
            return;
        }
        //All three at zero is a set of fields Unity was never given values for, rather than a wave
        //deliberately switched off. Setting any one of them means the numbers are meant as they stand,
        //so an amplitude of zero on its own still turns it off.
        if (amplitude == 0f && frequency == 0f && perCharacterStep == 0f)
        {
            amplitude = DefaultWaveAmplitude;
            frequency = DefaultWaveFrequency;
            perCharacterStep = DefaultWaveStep;
        }
        if (amplitude == 0f)
        {
            return;
        }
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        if (info == null || info.characterCount == 0)
        {
            return;
        }
        bool moved = false;
        foreach (Vector2Int range in ranges)
        {
            int end = Mathf.Min(range.x + range.y, info.characterCount);
            for (int i = range.x; i < end; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                //a space has no quad in the mesh, and a letter not yet typed out has nothing to lift
                if (!character.isVisible || i >= text.maxVisibleCharacters)
                {
                    continue;
                }
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int vertex = character.vertexIndex;
                Vector3 lift = new Vector3(0f, Mathf.Sin(time * frequency + i * perCharacterStep) * amplitude, 0f);
                vertices[vertex + 0] += lift;
                vertices[vertex + 1] += lift;
                vertices[vertex + 2] += lift;
                vertices[vertex + 3] += lift;
                moved = true;
            }
        }
        if (!moved)
        {
            return;
        }
        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
            text.UpdateGeometry(info.meshInfo[i].mesh, i);
        }
    }
}
