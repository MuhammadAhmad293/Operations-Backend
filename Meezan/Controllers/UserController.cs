using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs;
using Meezan.IServices.IService;

namespace Meezan.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class UserController : ControllerBase
    {
        private IUserService UserService { get; }
        public UserController(IUserService userService)
        {
            UserService = userService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Add(UserDto userDto)
        {
            return Ok(await UserService.Add(userDto));
        }
        [HttpPut]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(UserDto userDto)
        {
            return Ok(await UserService.Update(userDto));
        }
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
        {
            return Ok(await UserService.Delete(id));
        }
        [HttpGet("order/{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<UserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(int id)
        {
            return Ok(await UserService.GetById(id));
        }
        [HttpGet("getAll")]
        [ProducesResponseType(typeof(ResponseDto<List<UserDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await UserService.GetAll());
        }
    }
}
