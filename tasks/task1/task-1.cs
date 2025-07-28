//  TASK: Formulate a prompt for LLM so that it generates a correct unit test 
//      using xUnit, with clear checks for both conditions (isMember = true/false).

namespace Task1.Services;

public class DiscountResult
{
    public int DiscountPercent { get; set; }
    public int FinalAmount { get; set; }
    public int BonusPoints { get; set; }
}

public class OrderService
{
    public DiscountResult CalculateOrder(int totalAmount, bool isMember, int itemsCount)
    {
        if (totalAmount < 0)
            throw new ArgumentException("Amount must be non-negative", nameof(totalAmount));

        int discount;
        if (isMember && totalAmount > 1000)
            discount = 15;
        else if (isMember)
            discount = 10;
        else if (totalAmount > 5000)
            discount = 5;
        else
            discount = 0;

        var bonusPoints = (totalAmount / 100) * (isMember ? 2 : 1);

        return new DiscountResult
        {
            DiscountPercent = discount,
            FinalAmount = totalAmount * (100 - discount) / 100,
            BonusPoints = bonusPoints
        };
    }
}

namespace Task1.Tests.Services
{
    public class ShippingServiceTests
    {
        [Fact]
        public void CalculateShipping_ReturnsFree_WhenWeightUnderThreshold()
        {
            // Arrange
            var service = new ShippingService();
            decimal weight = 4.5m; // kg
            decimal expected = 0m;

            // Act
            var result = service.CalculateShipping(weight);

            // Assert
            result.Should().Be(expected, because: "for weight up to 5 kg delivery is free");
        }
    }
}
