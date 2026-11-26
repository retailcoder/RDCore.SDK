using RDCore.SDK.Extensibility.Diagnostics;
using System.Globalization;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The text of a finding's message (<see cref="AnalyzerMessages.Format"/>): the particulars in their placeholders, written in the culture of the user
/// interface, which the message's own text is in.
/// </summary>
[TestClass]
public sealed class AnalyzerMessagesTests
{
    [TestMethod]
    public void EachParticular_GoesInItsPlaceholder_WhateverTheirOrder()
        => Assert.AreEqual("Order binds from the body of Post.", AnalyzerMessages.Format("{1} binds from the body of {0}.", "Post", "Order"));

    [TestMethod]
    public void ANumberInAMessage_IsWrittenInTheCultureOfTheUserInterface_NotTheCurrentCulture()
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var french = CultureInfo.GetCultureInfo("fr-CA");
        var english = CultureInfo.GetCultureInfo("en-US");
        const string message = "{0:N1} octets";
        Assert.AreNotEqual(string.Format(english, message, 1234.5), string.Format(french, message, 1234.5), "the two cultures write the number apart");
        try
        {
            CultureInfo.CurrentCulture = english;
            CultureInfo.CurrentUICulture = french;

            Assert.AreEqual(string.Format(french, message, 1234.5), AnalyzerMessages.Format(message, 1234.5));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    [TestMethod]
    public void APlaceholderWithNoParticular_IsAFormatError()
        => Assert.ThrowsExactly<FormatException>(() => AnalyzerMessages.Format("{0} and {1}", "one"));
}
