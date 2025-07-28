using Task1.Services;
using FluentAssertions;

namespace Task1.Tests.Services
{
    public class OrderServiceTests
    {
        private readonly OrderService _orderService = new OrderService();

        [Fact]
        public void CalculateOrder_NonMemberSmallOrder_ReturnsNoDiscount()
        {
            // Arrange
            int totalAmount = 100;
            bool isMember = false;
            int itemsCount = 2;
            int expectedDiscount = 0;
            int expectedFinalAmount = 100;
            int expectedBonusPoints = 1; // 100 / 100 * 1

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_NonMemberLargeOrder_Returns5PercentDiscount()
        {
            // Arrange
            int totalAmount = 5001;
            bool isMember = false;
            int itemsCount = 5;
            int expectedDiscount = 5;
            int expectedFinalAmount = 4751; // 5001 * 0.95
            int expectedBonusPoints = 50; // 5001 / 100 * 1

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_MemberSmallOrder_Returns10PercentDiscount()
        {
            // Arrange
            int totalAmount = 500;
            bool isMember = true;
            int itemsCount = 3;
            int expectedDiscount = 10;
            int expectedFinalAmount = 450; // 500 * 0.9
            int expectedBonusPoints = 10; // 500 / 100 * 2

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_MemberMediumOrder_Returns10PercentDiscount()
        {
            // Arrange
            int totalAmount = 1000;
            bool isMember = true;
            int itemsCount = 5;
            int expectedDiscount = 10;
            int expectedFinalAmount = 900; // 1000 * 0.9
            int expectedBonusPoints = 20; // 1000 / 100 * 2

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_MemberLargeOrder_Returns15PercentDiscount()
        {
            // Arrange
            int totalAmount = 1001;
            bool isMember = true;
            int itemsCount = 10;
            int expectedDiscount = 15;
            int expectedFinalAmount = 851; // 1001 * 0.85
            int expectedBonusPoints = 20; // 1001 / 100 * 2

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_NegativeAmount_ThrowsArgumentException()
        {
            // Arrange
            int totalAmount = -100;
            bool isMember = true;
            int itemsCount = 2;

            // Act
            Action act = () => _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Amount must be non-negative*")
                .WithParameterName("totalAmount");
        }

        [Fact]
        public void CalculateOrder_ZeroAmount_ReturnsZeroValues()
        {
            // Arrange
            int totalAmount = 0;
            bool isMember = true;
            int itemsCount = 0;
            int expectedDiscount = 10; // Member gets 10% even on zero amount
            int expectedFinalAmount = 0;
            int expectedBonusPoints = 0;

            // Act
            var result = _orderService.CalculateOrder(totalAmount, isMember, itemsCount);

            // Assert
            result.Should().NotBeNull();
            result.DiscountPercent.Should().Be(expectedDiscount);
            result.FinalAmount.Should().Be(expectedFinalAmount);
            result.BonusPoints.Should().Be(expectedBonusPoints);
        }

        [Fact]
        public void CalculateOrder_ExactThresholdAmounts_ReturnsCorrectDiscounts()
        {
            // Test exactly at discount threshold boundaries

            // Non-member just above 5000 threshold
            var nonMemberAboveThreshold = _orderService.CalculateOrder(5001, false, 1);
            nonMemberAboveThreshold.DiscountPercent.Should().Be(5);

            // Non-member at threshold (should not get discount)
            var nonMemberAtThreshold = _orderService.CalculateOrder(5000, false, 1);
            nonMemberAtThreshold.DiscountPercent.Should().Be(0);

            // Member just above 1000 threshold
            var memberAboveThreshold = _orderService.CalculateOrder(1001, true, 1);
            memberAboveThreshold.DiscountPercent.Should().Be(15);

            // Member at threshold (should get 10% not 15%)
            var memberAtThreshold = _orderService.CalculateOrder(1000, true, 1);
            memberAtThreshold.DiscountPercent.Should().Be(10);
        }
    }
}