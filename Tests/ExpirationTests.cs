using NUnit.Framework;
using ProjectZeroLib.Utils;

namespace Tests
{
    [TestFixture]
    public class ExpirationTests
    {
        /// <summary>
        /// ���������� ��������� ������� ������� �������� 
        /// � ������ ���� ������� ������.
        /// </summary>
        [Test]
        public void GetExpirationDate_Test1()
        {
            var date = new DateTime(2025, 1, 1, 1, 1, 1);

            var result = Expiration.GetQuarterExpirationDate(date);

            Assert.That(result, Is.EqualTo(new DateTime(2025, 3, 28)));
        }

        /// <summary>
        /// ���������� ��������� ������� ���������� ��������
        /// � ������ ���� ���������� ������.
        /// </summary>
        [Test]
        public void GetExpirationDate_Test2()
        {
            var date = new DateTime(2024, 12, 1, 1, 1, 1);

            var result = Expiration.GetQuarterExpirationDate(date);

            Assert.That(result, Is.EqualTo(new DateTime(2024, 12, 27)));
        }

        /// <summary>
        /// ���������� ��������� ������� ������� �������� ���������� ����
        /// � ��������� ������� ���������� ������ ������.
        /// </summary>
        [Test]
        public void GetExpirationDate_Test3()
        {
            var date = new DateTime(2024, 12, 28, 1, 1, 1);

            var result = Expiration.GetQuarterExpirationDate(date);

            Assert.That(result, Is.EqualTo(new DateTime(2025, 3, 28)));
        }

        /// <summary>
        /// ���������� ��������� ������� ������� �������� ���������� ����
        /// � ��������� ������� ���������� ������.
        /// </summary>
        [Test]
        public void GetExpirationDate_Test4()
        {
            var date = new DateTime(2024, 12, 27, 1, 1, 1);

            var result = Expiration.GetQuarterExpirationDate(date);

            Assert.That(result, Is.EqualTo(new DateTime(2025, 3, 28)));
        }

        /// <summary>
        /// ���������� ��������� ������� ������� �������� ���������� ����
        /// � ��������� ������� ���������� ������.
        /// </summary>
        [Test]
        public void GetExpirationDate_Test5()
        {
            var date = new DateTime(2024, 12, 26, 1, 1, 1);

            var result = Expiration.GetQuarterExpirationDate(date);

            Assert.That(result, Is.EqualTo(new DateTime(2025, 3, 28)));
        }
    }
}
