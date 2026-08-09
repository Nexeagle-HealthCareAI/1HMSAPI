using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class MarSlotMatcherTests
    {
        private static readonly DateTime Slot = new DateTime(2026, 7, 1, 2, 30, 0, DateTimeKind.Utc);

        [Test]
        public void Match_CandidateExactlyAtSlot_Matches()
        {
            var id = Guid.NewGuid();
            var result = MarSlotMatcher.Match(
                new[] { Slot },
                new[] { new MarSlotMatcher.Candidate(id, Slot, Slot) });

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].MatchedId, Is.EqualTo(id));
        }

        [Test]
        public void Match_NoCandidateWithinTolerance_ReturnsUnmatchedSlot()
        {
            var id = Guid.NewGuid();
            var farAway = Slot.AddHours(2);
            var result = MarSlotMatcher.Match(
                new[] { Slot },
                new[] { new MarSlotMatcher.Candidate(id, farAway, farAway) });

            Assert.That(result[0].MatchedId, Is.Null);
        }

        [Test]
        public void Match_MultipleCandidatesWithinTolerance_NearestWins()
        {
            var near = Guid.NewGuid();
            var far = Guid.NewGuid();
            var result = MarSlotMatcher.Match(
                new[] { Slot },
                new[]
                {
                    new MarSlotMatcher.Candidate(far, Slot.AddMinutes(40), Slot.AddMinutes(40)),
                    new MarSlotMatcher.Candidate(near, Slot.AddMinutes(5), Slot.AddMinutes(5)),
                });

            Assert.That(result[0].MatchedId, Is.EqualTo(near));
        }

        [Test]
        public void Match_TiedDistance_LatestActedAtWins()
        {
            var older = Guid.NewGuid();
            var newer = Guid.NewGuid();
            var result = MarSlotMatcher.Match(
                new[] { Slot },
                new[]
                {
                    new MarSlotMatcher.Candidate(older, Slot.AddMinutes(10), Slot.AddMinutes(1)),
                    new MarSlotMatcher.Candidate(newer, Slot.AddMinutes(-10), Slot.AddMinutes(9)),
                });

            Assert.That(result[0].MatchedId, Is.EqualTo(newer));
        }

        [Test]
        public void Match_EachCandidateClaimedAtMostOnce()
        {
            var id = Guid.NewGuid();
            var slot2 = Slot.AddHours(1);
            var result = MarSlotMatcher.Match(
                new[] { Slot, slot2 },
                new[] { new MarSlotMatcher.Candidate(id, Slot, Slot) });

            Assert.That(result[0].MatchedId, Is.EqualTo(id));
            Assert.That(result[1].MatchedId, Is.Null);
        }

        [Test]
        public void GetClaimedIds_ReturnsOnlyMatchedIds()
        {
            var matchedId = Guid.NewGuid();
            var matches = new List<MarSlotMatcher.SlotMatch>
            {
                new(Slot, matchedId),
                new(Slot.AddHours(1), null),
            };

            var claimed = MarSlotMatcher.GetClaimedIds(matches);

            Assert.That(claimed, Is.EquivalentTo(new[] { matchedId }));
        }
    }
}
