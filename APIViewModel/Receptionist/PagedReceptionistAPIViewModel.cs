namespace APIViewModel.Receptionist;

public class PagedReceptionistAPIViewModel
{
    public List<ReceptionistListItemAPIViewModel> Items { get; set; }
        = new List<ReceptionistListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
