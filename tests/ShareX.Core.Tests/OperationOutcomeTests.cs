using System;
using ShareX.Core.Errors;
using Xunit;

namespace ShareX.Core.Tests
{
    public class OperationOutcomeTests
    {
        [Fact]
        public void Success_ExposesValue_AndIsSuccessTrue()
        {
            OperationOutcome<int> outcome = OperationOutcome<int>.Success(42);

            Assert.True(outcome.IsSuccess);
            Assert.False(outcome.IsFailure);
            Assert.Equal(42, outcome.Value);
            Assert.Null(outcome.Error);
        }

        [Fact]
        public void Failure_ExposesError_AndIsSuccessFalse()
        {
            var error = new TaskError(TaskErrorKind.PermissionDenied, "Screen recording denied.");
            OperationOutcome<int> outcome = OperationOutcome<int>.Failure(error);

            Assert.False(outcome.IsSuccess);
            Assert.True(outcome.IsFailure);
            Assert.Equal(error, outcome.Error);
        }

        [Fact]
        public void Value_OnFailure_Throws()
        {
            var error = new TaskError(TaskErrorKind.UserCancelled, "Cancelled.");
            OperationOutcome<string> outcome = OperationOutcome<string>.Failure(error);

            Assert.Throws<InvalidOperationException>(() => outcome.Value);
        }

        [Fact]
        public void Failure_WithNullError_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => OperationOutcome<int>.Failure(null!));
        }

        [Fact]
        public void Match_OnSuccess_InvokesSuccessBranchOnly()
        {
            OperationOutcome<int> outcome = OperationOutcome<int>.Success(7);

            string result = outcome.Match(
                onSuccess: v => $"ok:{v}",
                onFailure: e => throw new InvalidOperationException("Should not be called."));

            Assert.Equal("ok:7", result);
        }

        [Fact]
        public void Match_OnFailure_InvokesFailureBranchOnly()
        {
            var error = new TaskError(TaskErrorKind.EncoderFailure, "PNG encode failed.");
            OperationOutcome<int> outcome = OperationOutcome<int>.Failure(error);

            string result = outcome.Match(
                onSuccess: v => throw new InvalidOperationException("Should not be called."),
                onFailure: e => $"fail:{e.Kind}");

            Assert.Equal("fail:EncoderFailure", result);
        }

        [Fact]
        public void Equality_TwoSuccessOutcomes_WithSameValue_AreEqual()
        {
            OperationOutcome<int> a = OperationOutcome<int>.Success(5);
            OperationOutcome<int> b = OperationOutcome<int>.Success(5);

            Assert.Equal(a, b);
            Assert.True(a == b);
        }

        [Fact]
        public void Equality_SuccessAndFailure_AreNotEqual()
        {
            OperationOutcome<int> success = OperationOutcome<int>.Success(5);
            OperationOutcome<int> failure = OperationOutcome<int>.Failure(new TaskError(TaskErrorKind.InternalFailure, "x"));

            Assert.NotEqual(success, failure);
            Assert.True(success != failure);
        }
    }
}
