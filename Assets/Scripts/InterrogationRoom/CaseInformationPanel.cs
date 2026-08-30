using UnityEngine;

namespace InterrogationRoom
{
    //The police file: everything the department already knows, as opposed to everything the player works
    //out. It reads as a stack of pages turned one at a time - a page per suspect with their details and
    //the alibi they gave, then the autopsy report and whatever else the case came with.
    //
    //The suspect pages are not authored separately: they are the same profiles the phone dials and the
    //notebook names, so a suspect can never appear in the file under one name and be called under
    //another. Both kinds of page are set out the same way, as findings with the photographs beside them,
    //so the file reads as one document rather than as two shapes of page bound together.
    public class CaseInformationPanel : RoomPanel
    {
        [Header("Case file prefabs")]
        //the two columns a page of findings is laid out in
        [SerializeField] private FindingsRow findingsRowPrefab;
        //one line of a report: its name, and what it says
        [SerializeField] private CaseFinding findingPrefab;
        //one captioned photograph in the stack down the side of the page
        [SerializeField] private CasePlate platePrefab;

        private int pageIndex;

        protected override string Title
        {
            get { return "Case file - " + PageTitle(pageIndex) + "   (" + (pageIndex + 1) + "/" + PageCount + ")"; }
        }

        //the suspects first, in the order they are authored, then the loose pages behind them
        private int PageCount
        {
            get
            {
                int suspects = director != null && director.Suspects != null ? director.Suspects.Length : 0;
                int extras = director != null && director.CasePages != null ? director.CasePages.Length : 0;
                return Mathf.Max(1, suspects + extras);
            }
        }

        protected override void OnOpened()
        {
            //the file is picked up at the front each time rather than wherever it was last left open
            pageIndex = 0;
        }

        //the file has a front and a back, so the arrows come off the card at either end of it rather
        //than turning a page that is not there
        protected override bool CanGoBack
        {
            get { return pageIndex > 0; }
        }

        protected override void GoBack()
        {
            TurnPage(-1);
        }

        protected override bool CanGoForward
        {
            get { return pageIndex < PageCount - 1; }
        }

        protected override void GoForward()
        {
            TurnPage(1);
        }

        protected override void Populate()
        {
            SuspectProfile suspect = SuspectAt(pageIndex);
            if (suspect != null)
            {
                BuildSuspectPage(suspect);
                return;
            }
            CasePage page = PageAt(pageIndex);
            if (page != null)
            {
                BuildCasePage(page);
                return;
            }
            AddText("Nothing has been filed on this case yet.", PanelText.Dim);
        }

        //Their name is the one finding always on file; the rest appear as they are written, so a suspect
        //nothing is known about yet is a name and a face rather than a page of "no details on file".
        private void BuildSuspectPage(SuspectProfile suspect)
        {
            //the file names the person, not the handle the phone and clues use
            string name = string.IsNullOrEmpty(suspect.realName) ? suspect.displayName : suspect.realName;
            AddFindings(new CaseFact[]
            {
                new CaseFact
                {
                    label = "Name",
                    value = name,
                    image = suspect.portrait,
                    //every one of them is drawn standing, head to shoe
                    cropImage = true,
                    imageLabel = name
                },
                new CaseFact { label = "Profession", value = suspect.profession },
                new CaseFact { label = "Alibi", value = suspect.alibi },
                new CaseFact { label = "Autopsy report", value = suspect.autopsyReport }
            });
            //anything the department knows that is not one of the findings above
            if (!string.IsNullOrEmpty(suspect.information))
            {
                AddText(suspect.information, PanelText.Body);
            }
        }

        private void BuildCasePage(CasePage page)
        {
            bool written = page.facts != null && page.facts.Length > 0;
            //A page of findings is already titled by the file it is filed in, at the top of the card, so
            //saying it again over the findings is the same words twice and a line of the page gone.
            if (!written)
            {
                AddText(page.title, PanelText.Note);
            }
            AddFindings(page.facts);
            //a page written as findings has nothing else to say, and an empty run of text here would
            //still take a line of the page
            if (!string.IsNullOrEmpty(page.body))
            {
                AddText(page.body, PanelText.Body);
            }
        }

        //A finding with nothing written in it is not on file yet, and a blank line under its name says
        //less than leaving it out. Its picture, if it has one, is still a picture.
        private void AddFindings(CaseFact[] facts)
        {
            if (facts == null || facts.Length == 0)
            {
                return;
            }
            int lines = 0;
            int pictures = 0;
            foreach (CaseFact fact in facts)
            {
                if (fact == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(fact.value))
                {
                    lines++;
                }
                if (fact.image != null)
                {
                    pictures++;
                }
            }
            if (lines == 0 && pictures == 0)
            {
                return;
            }
            if (findingsRowPrefab == null || itemsParent == null)
            {
                Missing("Findings Row Prefab");
                return;
            }
            FindingsRow row = Instantiate(findingsRowPrefab, itemsParent);
            row.ShowColumns(lines > 0, pictures > 0);

            foreach (CaseFact fact in facts)
            {
                if (fact == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(fact.value) && row.Facts != null)
                {
                    if (findingPrefab == null)
                    {
                        Missing("Finding Prefab");
                        return;
                    }
                    Instantiate(findingPrefab, row.Facts).Set(fact.label, fact.value);
                }
                if (fact.image != null && row.Plates != null)
                {
                    if (platePrefab == null)
                    {
                        Missing("Plate Prefab");
                        return;
                    }
                    string caption = string.IsNullOrEmpty(fact.imageLabel) ? fact.label : fact.imageLabel;
                    Instantiate(platePrefab, row.Plates).Set(caption, fact.image, fact.cropImage);
                }
            }
        }

        //the file has a front and a back, so the arrows stop rather than wrapping round
        private void TurnPage(int step)
        {
            pageIndex = Mathf.Clamp(pageIndex + step, 0, PageCount - 1);
            Refresh();
        }

        private SuspectProfile SuspectAt(int index)
        {
            SuspectProfile[] suspects = director != null ? director.Suspects : null;
            if (suspects == null || index < 0 || index >= suspects.Length)
            {
                return null;
            }
            return suspects[index];
        }

        private CasePage PageAt(int index)
        {
            CasePage[] pages = director != null ? director.CasePages : null;
            int suspects = director != null && director.Suspects != null ? director.Suspects.Length : 0;
            int pageOffset = index - suspects;
            if (pages == null || pageOffset < 0 || pageOffset >= pages.Length)
            {
                return null;
            }
            return pages[pageOffset];
        }

        private string PageTitle(int index)
        {
            SuspectProfile suspect = SuspectAt(index);
            if (suspect != null)
            {
                return suspect.displayName;
            }
            CasePage page = PageAt(index);
            return page != null ? page.title : "empty";
        }
    }
}
