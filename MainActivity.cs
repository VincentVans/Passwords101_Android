using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Passwords101_Android
{
    [Activity(Label = "Passwords 101", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity
    {
        const string inputsKey = "inputsKey";
        const string specialCharKey = "specialChar";
        const string maxLengthKey = "maxLength";
        const int ignoreMaxLength = -1;
        private ICollection<string> inputsCollection;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Main);
            var prefs = GetPreferences(FileCreationMode.Private);
            inputsCollection = new HashSet<string>(prefs.GetStringSet(inputsKey, new string[] { }));
            var autoComplete = FindViewById<AutoCompleteTextView>(Resource.Id.autoCompleteTextView1);
            autoComplete.Threshold = 0;
            autoComplete.AfterTextChanged += AutoComplete_AfterTextChanged;
            RefreshAutoComplete();
            FindViewById<TextView>(Resource.Id.labelSpecialChar).Click += Switch_Label_Click;
            FindViewById<Switch>(Resource.Id.switch1).CheckedChange += Switch_CheckedChange;
            FindViewById<EditText>(Resource.Id.editTextMasterPassword).TextChanged += MasterPassword_TextChanged;
            FindViewById<Button>(Resource.Id.buttonGeneratePassword).Click += Generate_Click;
            FindViewById<Button>(Resource.Id.buttonCopyResult).Click += Copy_Click;
            FindViewById<TextView>(Resource.Id.textViewPasswordResult).TextChanged += ResultPassword_TextChanged;
        }

        private void RefreshAutoComplete()
        {
            var inputs = new string[inputsCollection.Count];
            inputsCollection.CopyTo(inputs, 0);
            var autoComplete = FindViewById<AutoCompleteTextView>(Resource.Id.autoCompleteTextView1);
            autoComplete.Adapter = new ArrayAdapter<string>(this, Resource.Layout.textView, inputs);
        }

        private void AutoComplete_AfterTextChanged(object sender, Android.Text.AfterTextChangedEventArgs e)
        {
            var autoComplete = FindViewById<AutoCompleteTextView>(Resource.Id.autoCompleteTextView1);
            var sharedPreferences = GetSharedPreferences(autoComplete.Text, FileCreationMode.Private);
            var specialChar = sharedPreferences.GetString(specialCharKey, "");
            var maxLength = sharedPreferences.GetInt(maxLengthKey, ignoreMaxLength);
            if (specialChar.Length > 0 || maxLength != ignoreMaxLength)
            {
                FindViewById<EditText>(Resource.Id.editTextSpecialChar).Text = specialChar;
                FindViewById<EditText>(Resource.Id.editTextMaxLength).Text = maxLength == ignoreMaxLength ? "" : maxLength.ToString();
                FindViewById<Switch>(Resource.Id.switch1).Checked = true;
            }
            else
            {
                FindViewById<EditText>(Resource.Id.editTextSpecialChar).Text = "!";
                FindViewById<EditText>(Resource.Id.editTextMaxLength).Text = "";
                FindViewById<Switch>(Resource.Id.switch1).Checked = false;
            }
        }

        private void Switch_Label_Click(object sender, EventArgs e)
        {
            var switchButton = FindViewById<Switch>(Resource.Id.switch1);
            switchButton.Checked = !switchButton.Checked;
        }

        private void MasterPassword_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            var text = string.Concat(e.Text);
            FindViewById<TextView>(Resource.Id.textViewHashCheck).Text = text.Length > 0 ? HashCheck(text) : GetString(Resource.String.labelCheckEmpty);
        }

        private string HashCheck(string pass)
        {
            return ByteToString(Pbkdf2(pass, Pbkdf2(pass, StringToByte("referenceCode"), 1, 32), 10, 2)).Substring(0, 2).Replace("=", "");
        }

        private void ResultPassword_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            FindViewById<Button>(Resource.Id.buttonCopyResult).Enabled = e.AfterCount > 0;
        }

        private void Copy_Click(object sender, EventArgs e)
        {
            var manager = (ClipboardManager)GetSystemService(ClipboardService);
            var result = FindViewById<TextView>(Resource.Id.textViewPasswordResult);
            manager.PrimaryClip = ClipData.NewPlainText("Generated password", result.Text);
        }

        private void Generate_Click(object sender, EventArgs e)
        {
            var progress = FindViewById<ProgressBar>(Resource.Id.progressBar1);
            progress.Indeterminate = true;
            var result = FindViewById<TextView>(Resource.Id.textViewPasswordResult);
            var input = FindViewById<TextView>(Resource.Id.autoCompleteTextView1).Text.ToLowerInvariant();
            var password = FindViewById<EditText>(Resource.Id.editTextMasterPassword).Text;
            var useAdvanced = FindViewById<Switch>(Resource.Id.switch1).Checked;
            string specialChar;
            int maxLength;
            if (useAdvanced)
            {
                specialChar = FindViewById<TextView>(Resource.Id.editTextSpecialChar).Text;
                if (!Int32.TryParse(FindViewById<EditText>(Resource.Id.editTextMaxLength).Text, out maxLength))
                {
                    maxLength = ignoreMaxLength;
                }
            }
            else
            {
                specialChar = "";
                maxLength = ignoreMaxLength;
            }
            Task.Factory.StartNew(() =>
            {
                string hash;
                var salt = Pbkdf2(password, StringToByte(input), 1, 32);
                var generated = Pbkdf2(password, salt, 50000, 12);
                if (maxLength != ignoreMaxLength)
                {
                    hash = Truncate(Truncate(ByteToString(generated), maxLength - specialChar.Length) + specialChar, maxLength).PadRight(maxLength, 'a');
                }
                else
                {
                    hash = ByteToString(generated) + specialChar;
                }
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(generated, 0, generated.Length);
                RunOnUiThread(() =>
                {
                    result.Text = hash;
                    progress.Indeterminate = false;
                    UpdateStorage(input, specialChar, maxLength);
                });
            });
        }

        private string Truncate(string input, int length)
        {
            return input.Substring(0, Math.Min(input.Length, Math.Max(0, length)));
        }

        private void UpdateStorage(string input, string specialChar, int maxLength)
        {
            var prefs = GetPreferences(FileCreationMode.Private);
            inputsCollection.Add(input);
            var edit = prefs.Edit();
            edit.PutStringSet(inputsKey, inputsCollection);
            edit.Commit();
            var sharedPreferences = GetSharedPreferences(input, FileCreationMode.Private);
            var sharedEdit = sharedPreferences.Edit();
            sharedEdit.PutString(specialCharKey, specialChar);
            sharedEdit.PutInt(maxLengthKey, maxLength);
            sharedEdit.Commit();
            RefreshAutoComplete();
        }

        private void Switch_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            var visibility = e.IsChecked ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
            FindViewById<TextView>(Resource.Id.labelSpecialCharInput).Visibility = visibility;
            FindViewById<EditText>(Resource.Id.editTextSpecialChar).Visibility = visibility;
            FindViewById<TextView>(Resource.Id.labelMaxLength).Visibility = visibility;
            FindViewById<EditText>(Resource.Id.editTextMaxLength).Visibility = visibility;
        }

        static byte[] StringToByte(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        static string ByteToString(byte[] array)
        {
            return Convert.ToBase64String(array).Replace('+', 'K').Replace('/', 'S');
        }

        static byte[] Pbkdf2(string password, byte[] salt, int iterationCount, int derivedKeyLength)
        {
            // Pbkdf2 is defined in NIST SP800-132:
            // http://csrc.nist.gov/publications/nistpubs/800-132/nist-sp800-132.pdf
            var passwordBytes = StringToByte(password);
            using (var hmac = new System.Security.Cryptography.HMACSHA256(passwordBytes))
            {
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                int hashLength = hmac.HashSize / 8;
                if ((hmac.HashSize & 7) != 0)
                {
                    hashLength++;
                }
                if (derivedKeyLength > (0xFFFFFFFFL * hashLength) || derivedKeyLength < 0)
                {
                    throw new ArgumentOutOfRangeException("derivedKeyLength");
                }
                int keyLength = derivedKeyLength / hashLength;
                if (derivedKeyLength % hashLength != 0)
                {
                    keyLength++;
                }
                var saltWithIdx = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, saltWithIdx, 0, salt.Length);
                using (var ms = new System.IO.MemoryStream())
                {
                    for (var block = 1; block <= keyLength; block++)
                    {
                        saltWithIdx[saltWithIdx.Length - 4] = (byte)(block >> 24);
                        saltWithIdx[saltWithIdx.Length - 3] = (byte)(block >> 16);
                        saltWithIdx[saltWithIdx.Length - 2] = (byte)(block >> 8);
                        saltWithIdx[saltWithIdx.Length - 1] = (byte)(block);
                        var u = hmac.ComputeHash(saltWithIdx);
                        Array.Clear(saltWithIdx, salt.Length, 4);
                        var f = u;
                        for (var j = 1; j < iterationCount; j++)
                        {
                            u = hmac.ComputeHash(u);
                            for (var k = 0; k < f.Length; k++)
                            {
                                f[k] ^= u[k];
                            }
                        }
                        ms.Write(f, 0, f.Length);
                        Array.Clear(u, 0, u.Length);
                        Array.Clear(f, 0, f.Length);
                    }
                    Array.Clear(saltWithIdx, 0, saltWithIdx.Length);
                    var derivedKey = new byte[derivedKeyLength];
                    //Set Key
                    ms.Position = 0;
                    ms.Read(derivedKey, 0, derivedKeyLength);
                    //Clear MemoryStream
                    ms.Position = 0;
                    for (long i = 0; i < ms.Length; i++)
                    {
                        ms.WriteByte(0);
                    }
                    return derivedKey;
                }
            }
        }
    }
}