﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using LMS.Dto;
using LMS.Identity;
using LMS.Business.Services;

namespace LMS.Socket
{
    public class SessionHub : Hub
    {
        private const string AdminGroup = "Admins";

        private static readonly ConcurrentDictionary<string, SessionUserDTO> users =
            new ConcurrentDictionary<string, SessionUserDTO>();

        private static readonly ConcurrentDictionary<int, Timer> sessionClearTimers =
            new ConcurrentDictionary<int, Timer>();

        private readonly TestSessionService testSessionService;

        public SessionHub(TestSessionService sessionService)
        {
            testSessionService = sessionService;
        }

        [Authorize(Roles = "admin, moderator")]
        public Task Ban(int sessionId, string userId)
        {
            return Clients.User(userId).SendAsync(nameof(Ban), sessionId);
        }

        [Authorize]
        public Task UpdateState(TestTasksStateDTO state)
        {
            var user = UpdateStateForCurrentUser(state);

            return Clients.Groups(AdminGroup).SendAsync(nameof(UpdateState), user);
        }

        [Authorize]
        public Task Complete(TestTasksStateDTO state)
        {
            var user = UpdateStateForCurrentUser(state);
            user.StartTime = user.StartTime ?? DateTimeOffset.Now;
            user.Duration = DateTimeOffset.Now - user.StartTime.Value;

            return Clients.Groups(AdminGroup).SendAsync(nameof(Complete), user);
        }

        [Authorize]
        public Task Start()
        {
            if (!(Context.User.GetUserId() is string userId))
            {
                throw new UnauthorizedAccessException();
            }

            var session = testSessionService.FindByUserId(userId);
            var dueEndTime = session.EndTime - DateTimeOffset.Now;
            var sessionId = session?.Id;
            if (!sessionId.HasValue
                || dueEndTime <= TimeSpan.Zero)
            {
                throw new UnauthorizedAccessException();
            }

            var user = users.AddOrUpdate(userId,
                (id) => new SessionUserDTO
                {
                    Id = id,
                    SessionId = sessionId.Value,
                    StartTime = DateTimeOffset.Now
                },
                (_, u) =>
                {
                    u.StartTime = DateTimeOffset.Now;
                    u.Duration = null;
                    u.SessionId = sessionId.Value;
                    return u;
                });

            sessionClearTimers.GetOrAdd(user.SessionId, sessId =>
                new Timer(ClearSessionCallback, sessId, (long)dueEndTime.TotalMilliseconds, Timeout.Infinite));

            return Clients.Groups(AdminGroup).SendAsync(nameof(Start), user);
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User.IsInRole(Roles.Admin)
                || Context.User.IsInRole(Roles.Moderator)
                || Context.User.IsInRole(Roles.Reviewer))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");

                if (users.Any())
                {
                    await Clients.Caller.SendAsync("Users", users.Values);
                }

                await base.OnConnectedAsync();
            }
            else
            {
                if (!(Context.User.GetUserId() is string userId))
                {
                    throw new UnauthorizedAccessException();
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, "Users");

                var user = users.GetOrAdd(userId,
                    (id) => new SessionUserDTO { Id = id });

                await Clients.Group(AdminGroup).SendAsync("UserConnected", user);

                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (Context.User.IsInRole(Roles.Admin)
                || Context.User.IsInRole(Roles.Moderator)
                || Context.User.IsInRole(Roles.Reviewer))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
                await base.OnDisconnectedAsync(exception);
            }
            else
            {
                if (!(Context.User.GetUserId() is string userId))
                {
                    throw new UnauthorizedAccessException();
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Users");

                if (users.TryGetValue(userId, out var user))
                {
                    await Clients.Group(AdminGroup).SendAsync("UserDisconnected", user);
                }

                await base.OnDisconnectedAsync(exception);
            }
        }

        private SessionUserDTO UpdateStateForCurrentUser(TestTasksStateDTO state)
        {
            if (!(Context.User.GetUserId() is string userId))
            {
                throw new UnauthorizedAccessException();
            }
            if (!users.TryGetValue(userId, out var user))
            {
                throw new InvalidOperationException("Attempt to update not connected user");
            }
            user.TasksState = state;

            users.TryUpdate(userId, user, user);

            return user;
        }

        private void ClearSessionCallback(object state)
        {
            var sessionId = (int)state;
            var removedUsers = new List<SessionUserDTO>(users.Count);

            foreach (var user in users.Values)
            {
                if (user.SessionId == sessionId
                    && users.TryRemove(user.Id, out var removeUser))
                {
                    removedUsers.Add(removeUser);
                }
            }
            if (sessionClearTimers.TryRemove(sessionId, out var timer))
            {
                timer.Dispose();
            }
        }
    }
}
