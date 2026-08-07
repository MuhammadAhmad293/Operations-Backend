using Common.Dto;
using Meezan.Dto.DTOs;

namespace Meezan.IServices.IService
{
    public interface IUserService
    {
        Task<ResponseDto<EmptyResponseDto>> Add(UserDto userDto);
        Task<ResponseDto<EmptyResponseDto>> Update(UserDto userDto);
        Task<ResponseDto<EmptyResponseDto>> Delete(int id);
        Task<ResponseDto<UserDto>> GetById(int id);
        Task<ResponseDto<List<UserDto>>> GetAll();
    }
}
