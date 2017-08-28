using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyCodeCamp.Controllers;
using MyCodeCamp.Data.Entities;

namespace MyCodeCamp.Models
{
    public class TalkLinksResolver : IValueResolver<Talk, TalkModel, ICollection<LinkModel>>
    {
        private IHttpContextAccessor _httpContextAccessor;

        public TalkLinksResolver(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ICollection<LinkModel> Resolve(Talk source, TalkModel destination, ICollection<LinkModel> destMember,
            ResolutionContext context)
        {
            var helper = (IUrlHelper) _httpContextAccessor.HttpContext.Items[BaseController.URLHELPER];

            return new List<LinkModel>()
            {
                new LinkModel()
                {
                    Rel = "Self",
                    Href = helper.Link("GetTalk",
                        new {moniker = source.Speaker.Camp.Moniker, speakerId = source.Speaker.Id, id = source.Id})
                },
                new LinkModel()
                {
                    Rel = "Update",
                    Href = helper.Link("UpdateTalk",
                        new {moniker = source.Speaker.Camp.Moniker, speakerId = source.Speaker.Id, id = source.Id}),
                    Verb = "PUT"
                },
                new LinkModel()
                {
                    Rel = "Speaker",
                    Href = helper.Link("SpeakerGet",
                        new {moniker = source.Speaker.Camp.Moniker, id = source.Speaker.Id}),
                    Verb = "GET"
                }
            };
        }
    }
}