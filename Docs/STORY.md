# Red Herring — Story Bible

**Status:** design document. Everything here is grounded in art and scenes that already
exist in `Assets/`. Where a beat needs art that does not exist yet, it is marked **[NEW ART]**
and kept as small as possible.

---

## 1. The premise

You are a waiter. Six hours ago you were carrying a charcuterie board through the Baron's
dining hall. Now you are in a grey room across a table from a detective, and the knife that
killed the other waiter came off your board.

You do not get to investigate. You get to *remember*. Each time the detective pushes, you
replay a fragment of the night — 11 PM, 12 AM, 1 AM — and look for the one detail that
contradicts the story being built around you.

The game is called Red Herring because in each act the obvious answer is a lie:

| Act | The obvious suspect | The actual finding |
|---|---|---|
| I | The pianist (scarred hands) | The Chief of Police took the knife |
| II | The widow and her driver (they have the ring) | The ring came off the Baron's hand |
| III | — | The Baron killed him |

---

## 2. Cast

All of these have finished sprites in `Assets/Sprites/Characters/`.

| Character | Sprite | Role |
|---|---|---|
| **You** | `character-Player.png` | Waiter. Name entered at the main menu. |
| **Elias Kerr** | `character-victim.png` | The other waiter. Nineteen. Dead in the pantry stair. |
| **Baron Aldric Voss** | `character-Baron.png` | Host. Old money that is fifteen years old. **The killer.** |
| **Mireille Anselm** | `character-Widow.png` | The widow. Green hair, lavender gown, pearl bracelet. Came to this party for one reason. |
| **Ruben** | `character-Henchman.png` | The widow's driver. Grey suit, very tall, very polite. |
| **Gaspard** | `character-Pianist.png` | House pianist, thirty years in these rooms. Rose in the pocket. (Sprite reads androgynous — written as *they/them*; swap freely.) |
| **Chief Halloran** | `ChiefOfficer.png` | Chief of police. Off duty, at the party, on the Baron's payroll. Hands always behind his back. |
| **Det. Wren** | `character-Interrogator.png` | Runs your interrogation. Not corrupt — just wrong, and in a hurry. |
| **Mayor Delacroix** | `character-Mayor.png` | Guest of honour. Never speaks. He is the clock. |

---

## 3. What actually happened

### Fifteen years ago
Julien Anselm, a shipping man, was killed in what the file calls a robbery. His personal
secretary and fixer was a nobody named Aldric Voss. Voss did it. He took Anselm's signet
ring — a two-headed crow — off the body, and he has worn it every day since, because nobody
who could recognise it has ever been close enough to his right hand.

Voss inherited the contracts, bought a title, and became the Baron.

Mireille Anselm has spent fifteen years getting invited to the right parties so she can look
at people's hands.

### Tonight

| Time | Beat | Scene art |
|---|---|---|
| 11:00 PM | **The toast.** The Baron raises his glass with his right hand. The ring is plain to see. Across the room the widow stops talking mid-sentence. | `BaronDiningRoom1.png` |
| 11:40 PM | The widow tells Ruben: *get me that ring tonight.* | — |
| 12:00 AM | **The spill.** Elias is jostled and puts a glass of red down the Baron's front. The Baron grabs him. Ruben steps in to "help" and works at the Baron's hand — and fails, but the ring comes loose and drops into the wine on Elias's tray. Elias palms it while he mops up. **The Baron thinks Ruben took it. Ruben thinks he missed.** Only Elias knows. | `BaronDiningRoom2.png` |
| 12:20 AM | The Baron finds his hand bare. He cannot shout about it — the Mayor is ten feet away, and the ring is a murder trophy. He sends Halloran to find it quietly. | — |
| 12:30 AM | Halloran shakes down the staff. He lifts a boning knife off **your** board — he is unarmed at a private function and he needs to frighten, not shoot. He corners Elias in the service corridor. | `obj-knife_clean.png` |
| 12:40 AM | **Gaspard intervenes** and grabs the blade barehanded. Palm and three fingers laid open. Halloran drops the knife, throws them off; Elias runs. **The knife stays on the corridor floor.** | **[NEW ART]** bandaged-hand variant, or reuse `Hands.png` |
| 12:45 AM | Ruben finds Elias. Offers money, asks nothing. Elias sells the ring. | — |
| 1:00 AM | **The handoff.** Ruben gives the widow a small case. Gaspard is at the piano playing badly with a napkin round one hand. Halloran has his hands behind his back. Your board is one knife short. | `BaronDiningRoom3.png` |
| 1:20 AM | The Baron goes for the knife himself and finds Elias on the pantry stair. Elias tries to bargain — and says the word *Anselm*. That is the moment the Baron decides. | — |
| 1:25 AM | The Baron kills him with the knife already on the floor. Yours. | `Garage-deathScene.png` |
| 1:35 AM | Halloran finds the body and recognises the knife. If the blade is traced to him he is finished. So he builds a case in five minutes: staff knife, staff boy, no alibi. He bags it and arrests you. | `obj-knife-bloody.png`, `obj-knife-inzip.png` |
| 3:00 AM | Wren asks the questions. Halloran watches through the glass and tells him what to ask. | `MainScene` |

**The frame story's engine:** the man who needs you convicted is the man who took your knife.
That is why the interrogation is rigged, and it is the first thing you prove.

---

## 4. The three locks

Your instinct was right — knife, then ring, then motive. What the original version was
missing is that the three were *parallel*, not a chain. Solving the knife told you nothing
about the ring. The fix is that **each answer hands you the next question**, and it costs
almost nothing to write:

### Lock 1 — The knife
> *How did a knife get from your board to Elias's chest?*

- **Autopsy:** boning knife, 15 cm, matches the set issued with your charcuterie board.
- **Memory C (1 AM):** count the knives on your board. One short. *(Nice diegetic mechanic — the board is already built in `GameScene`.)*
- **Memory C:** Gaspard's hand is wrapped. The cuts run **across the palm and the inside of the fingers** — you do not get that grabbing a piano, you get it grabbing a blade.
- **Memory C:** Halloran, hands behind his back, in a room where he claims he never went past the hall.
- **Present:** Halloran keeps his right hand in his pocket the entire interrogation.

**Confront Halloran.** He does not break on murder — he breaks on the *knife*, because he
can survive theft and cannot survive the weapon. And to get out from under it he gives you
something better:

> "He'd lost something. He wanted it found quiet. That's all it ever was."

→ **You now know there was a search that night, for an object.**

### Lock 2 — The ring
> *What was Halloran looking for, and where did it go?*

- **Autopsy:** a pale band of untanned skin on Elias's right ring finger. No ring on the
  body. He wore one — for a few hours, that night, for the first time.
- **Memory A (11 PM):** the Baron's right hand at the toast. Gold signet. *(Already drawn — `Hands.png` hand #4 and `BaronDiningRoom2.png`.)*
- **Memory B (12 AM):** Ruben's hand on the Baron's chest during the spill. And after — no ring.
- **Memory C (1 AM):** the widow's hands were bare at 11. At 1 there is a gold band on her
  finger and a small case in her palm. *(Already drawn — `BaronDiningRoom3.png`.)*

**Confront the widow.** She does not deny anything. She turns it around:

> "You've had it in your hand. Did you look at it? There's a crow on it. Two heads."
> "That ring was on my husband's finger the night he died. It was not on it the morning after."

→ **You now know the ring is Julien Anselm's, and it was on the Baron's hand.**

### Lock 3 — The motive
> *Why does a man like that kill a waiter?*

Because the boy was carrying, in his apron pocket, the only physical proof that the Baron
murdered Julien Anselm — and because the boy was smart enough to say the name out loud.

- The widow's account of the crest.
- **Memory A, the toast itself.** The Baron says the name *Anselm* in his first speech, in the
  first ten minutes of the game, and the player has no idea it matters. Going back and hearing
  it again is the whole payoff.
- **Memory C:** the Baron's right hand is bare, and there is red down his shirt that you were
  told was wine.

**Accuse the Baron.**

---

## 5. What I changed and why

Six problems, and what fixes them. Four of these are things you already half-had.

1. **The links were not linked.** Fixed above: Halloran's confession *creates* the ring
   question; the widow's answer *creates* the motive question.

2. **"How did the waiter get the ring?" — your open question.** He didn't own it. It came
   off the Baron's hand during the spill and landed on his tray. This is the important one,
   because it also fixes:

3. **"Why would the Baron kill a waiter?" — your other open question.** Your instinct
   (*the Baron worked for the widow's husband, killed him, the ring is involved*) was
   correct. It only failed because the ring started on the victim. Put the ring on the
   **Baron's** hand and everything locks: it explains how a waiter got it, why it is missing
   from the body, why the widow wants it, why the Baron cannot ask for it publicly, and why
   he has to kill for it. **And your art already shows the Baron wearing a signet ring** —
   `BaronDiningRoom2.png` and hand #4 of `Hands.png`. The solution is already painted.

4. **Halloran had no reason to fight the pianist.** He wasn't fighting Gaspard — he was
   threatening Elias, and Gaspard put a hand on the blade to stop him. That gives the scars
   a cause, gives Gaspard a reason to lie to you at first (they are terrified), and turns
   the "obvious suspect with the bloody hands" into the night's most decent person.

5. **The Mayor was unused.** He never speaks. He is the timer: the Baron cannot make a scene
   with the Mayor in the room, and Wren needs a name before the Mayor wakes up. One line
   sells it.

6. **You need a reason not to just tell the truth.** You have three fragments of a nine-hour
   shift, you never saw the murder, and the only man who can place you elsewhere is the man
   filling out your arrest sheet.

---

## 6. Mechanic recommendation: look at the hands

You have `Assets/Sprites/Objects/Hands.png` — six forearms, sorted by cuff, one of them
wearing the gold ring, one with a pearl bracelet. Build the game's examine verb around it.

Every mystery in this story lives on a hand:

- the ring is **on a hand**
- the defensive cut is **on a hand**
- the missing knife was **in a hand**
- Halloran hides **his hands**
- the widow came to this party to look at **one man's hand**
- and the interrogator's first gesture — `character-Interrogator.png` — is an **open palm on
  the table**

Make the memory scenes work by clicking hands and logging what you see into `obj-noteBook.png`.
One verb, one asset that already exists, and it unifies all three acts. Open the game on the
detective's open hand and close it on the Baron's bare one.

---

## 7. Scope check (it is a jam)

What is here is: 3 memory scenes (3 backgrounds, already painted), 4 confrontations
(Halloran, Gaspard, Ruben/widow, Baron), 1 autopsy document, 1 suspect list, 3 act-break
locks. That is roughly an hour of play and a weekend of implementation on top of what exists.

**Do not add a fourth mystery.** If you have to cut, cut in this order:

1. Cut Ruben as a separate confrontation — fold him into the widow's scene.
2. Cut the knife-counting minigame; make the missing knife a single click.
3. Cut Gaspard's confrontation; put the hand reveal in Memory C and let the player infer it.

Never cut: the toast line in Memory A, the ring on the Baron's hand in Memory B, or the
widow's crest speech. Those three are the whole game.
