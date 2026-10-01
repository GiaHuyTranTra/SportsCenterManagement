namespace APIViewModel.Coach;

public class PagedCoachAPIViewModel
{
    public List<CoachListItemAPIViewModel> Items { get; set; }
        = new List<CoachListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
