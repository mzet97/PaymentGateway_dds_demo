using System.Text.RegularExpressions;

namespace PaymentGateway.Domain.ValueObjects;

public readonly partial record struct CustomerInfo
{
    public string Email { get; }
    public string Name { get; }
    public string Document { get; }
    public string? Ip { get; }
    public string? Phone { get; }

    [GeneratedRegex(@"^[\w\.-]+@[\w\.-]+\.\w+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public CustomerInfo(
        string email,
        string name,
        string document,
        string? ip = null,
        string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (!EmailRegex().IsMatch(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(document))
            throw new ArgumentException("Document is required", nameof(document));

        Email = email;
        Name = name;
        Document = NormalizeDocument(document);
        Ip = ip;
        Phone = phone;
    }

    private static string NormalizeDocument(string document)
    {
        return Regex.Replace(document, @"[^\d]", "");
    }

    public bool IsValidIpAddress()
    {
        if (string.IsNullOrWhiteSpace(Ip))
            return true;

        return System.Net.IPAddress.TryParse(Ip, out _);
    }

    public bool IsPrivateIpAddress()
    {
        if (string.IsNullOrWhiteSpace(Ip))
            return false;

        if (!System.Net.IPAddress.TryParse(Ip, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    public string GetDocumentType()
    {
        var doc = Document.Replace(".", "").Replace("-", "");
        return doc.Length == 11 ? "CPF" : "CNPJ";
    }

    public bool IsValidDocument()
    {
        var doc = Document.Replace(".", "").Replace("-", "");

        if (doc.Length == 11)
            return ValidateCpf(doc);

        if (doc.Length == 14)
            return ValidateCnpj(doc);

        return false;
    }

    private static bool ValidateCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
            return false;

        int[] multipliers1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multipliers2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCpf = cpf[..9];
        var sum = 0;

        for (int i = 0; i < 9; i++)
            sum += int.Parse(tempCpf[i].ToString()) * multipliers1[i];

        var remainder = sum % 11;
        var digit1 = remainder < 2 ? 0 : 11 - remainder;

        tempCpf += digit1;
        sum = 0;

        for (int i = 0; i < 10; i++)
            sum += int.Parse(tempCpf[i].ToString()) * multipliers2[i];

        remainder = sum % 11;
        var digit2 = remainder < 2 ? 0 : 11 - remainder;

        return cpf[9].ToString() == digit1.ToString() && cpf[10].ToString() == digit2.ToString();
    }

    private static bool ValidateCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1)
            return false;

        int[] multipliers1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multipliers2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCnpj = cnpj[..12];
        var sum = 0;

        for (int i = 0; i < 12; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multipliers1[i];

        var remainder = sum % 11;
        var digit1 = remainder < 2 ? 0 : 11 - remainder;

        tempCnpj += digit1;
        sum = 0;

        for (int i = 0; i < 13; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multipliers2[i];

        remainder = sum % 11;
        var digit2 = remainder < 2 ? 0 : 11 - remainder;

        return cnpj[12].ToString() == digit1.ToString() && cnpj[13].ToString() == digit2.ToString();
    }
}
