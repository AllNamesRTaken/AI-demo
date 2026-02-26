using AiDemo.Contracts.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AvaloniaApp.ViewModels;

public sealed partial class ItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private DateTime? _updatedAt;

    [ObservableProperty]
    private Guid _createdByUserId;

    public static ItemViewModel FromDto(ItemDto dto)
    {
        return new ItemViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            CreatedByUserId = dto.CreatedByUserId
        };
    }

    public UpdateItemDto ToUpdateDto()
    {
        return new UpdateItemDto(Id, Name, Description);
    }
}
