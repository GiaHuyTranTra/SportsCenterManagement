using System.Collections.Generic;

namespace APIViewModel.Receptionist;

public class PagedReceptionistResultAPIViewModel
{
    public List<ReceptionistListItemAPIViewModel> Items { get; set; } = new List<ReceptionistListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
