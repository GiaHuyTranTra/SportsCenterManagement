using APIViewModel.Homework;

namespace Services.HomeworkAssignmentService;

public enum AssignHomeworkResult
{
    Success,
    InvalidRequest,
    TargetNotFoundOrForbidden,
    TargetHasNoMembers
}

public interface IHomeworkAssignmentService
{
    Task<(AssignHomeworkResult Result, IReadOnlyList<HomeworkAssignmentAPIViewModel> Items)> AssignAsync(
        string coachId,
        CreateHomeworkAssignmentAPIViewModel request);

    Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetMemberHistoryAsync(string memberId);
    Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetCoachHistoryAsync(string coachId);
}
