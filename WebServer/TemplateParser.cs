using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;


/*
 * ADD REFERENCES:
 *  -- System.Web
 *  -- System.Web.Extensions
 * */


namespace WebServer

{
    class TemplateParser
    {
        /// <summary>
        /// Конструктор. Создает экземпляр объекта TEvaluateExpression.
        /// Опционально можно передать словарь с готовым набором заполненных переменных.
        /// </summary>
        /// <param name="values"></param>
        public TemplateParser(Dictionary<string, object> extValues = null)
        {
            this.extVals = extValues;
            this.intVals = new Dictionary<string, object>();

            this.templateElements = new List<_Template_Element>();
        }


        /// <summary>
        /// Словарь внутренних переменных
        /// </summary>    
        private Dictionary<string, object> intVals;


        /// <summary>
        /// Ссылка на словарь внешних переменных
        /// </summary>
        private Dictionary<string, object> extVals;


        /// <summary>
        /// Цепочка подготовленных элементов разбора шаблона.
        /// </summary>
        private List<_Template_Element> templateElements;

        /// <summary>
        /// Сохраняет строку в указанном файле. Старый файл перезаписывается.
        /// </summary>
        /// <param name="filename">Имя файла.</param>
        /// <param name="data">Строковые данные для сохранения.</param>
        /// <param name="encoding">Кодировка. По умолчанию UTF-8</param>
        /// <returns>True - если сохраненно успешно, иначе false.</returns>
        public bool SaveToFile(string filename, string data, Encoding encoding = null)
        {
            try
            {
                if (encoding != null)
                    File.WriteAllText(filename, data, encoding);
                else
                    File.WriteAllText(filename, data, Encoding.UTF8);
            }
            catch
            {
                return false;
            }

            return true;
        }


        /// <summary>
        /// Собрать строку на базе текстового ресурса приложения с шаблоном.
        /// </summary>
        /// <param name="resourceName">Имя ресурса (Properties.Resources....)</param>
        /// <param name="data">Словарь с переменными</param>
        /// <returns>Обработанный шаблон.</returns>
        public string ParseFromResource(string resource, Dictionary<string, object> data = null)
        {
            return this.ParseFromString(resource, data);
        }

        /// <summary>
        /// Собрать строку на базе файла с шаблоном.
        /// </summary>
        /// <param name="filename">Имя файла (включая путь)</param>
        /// <param name="data">Словарь с переменными</param>
        /// <param name="encoding">Кодировка. По умолчанию UTF-8</param>
        /// <returns>Обработанный шаблон.</returns>
        public string ParseFromFile(string filename, Dictionary<string, object> data = null, Encoding encoding = null)
        {
            if (data != null)
            {
                this.extVals = data;
            }

            if (!File.Exists(filename))
            {
                return $"<-- ERROR: FILE IS NOT EXISTS '{Path.GetFileName(filename)}' -->";
            }

            try
            {
                if (encoding == null)
                {
                    return this.ParseFromString(File.ReadAllText(filename, Encoding.UTF8));
                }
                else
                {
                    return this.ParseFromString(File.ReadAllText(filename, encoding));
                }
            }
            catch
            {
                return $"<-- ERROR: CANNOT READ TEXT FROM FILE '{Path.GetFileName(filename)}' -->";
            }
        }

        /// <summary>
        /// Собрать строку на базе входной строки шаблона.
        /// </summary>
        /// <param name="template">Строка с шаблоном</param>
        /// <param name="data">Словарь с переменными</param>
        /// <returns>Обработанный шаблон.</returns>        
        public string ParseFromString(string template, Dictionary<string, object> data = null)
        {
            if (data != null)
            {
                this.extVals = data;
            }

            try
            {
                // --1-- Первичная подготовка шаблона. Разбор на элементы. --------------------------------------

                // Вывести все совпадения: {{}} и {%%}. Первая группа - тип: {-переменная, %-команда.
                // Вторая группа - само выражение
                MatchCollection mm = Regex.Matches(template, @"{([{|%])(.+?)[}|%]}");

                // вспомогательные переменные
                int lastBlockStartPosition = 0;
                TBaseStack stack_for = new TBaseStack();
                TBaseStack stack_if = new TBaseStack();

                // управление блоками
                templateElements.Clear();
                int templateElementCurrentNr = 0;

                foreach (Match m in mm)
                {
                    if (m.Index != lastBlockStartPosition)
                    {
                        // перед этим был кусок текста, его надо сохранить в неизменном виде
                        _Template_Element el = new _Template_Element("T", templateElementCurrentNr);
                        el.expression = template.Substring(lastBlockStartPosition, m.Index - lastBlockStartPosition);
                        templateElements.Add(el);

                        templateElementCurrentNr++;
                    }

                    lastBlockStartPosition = m.Index + m.Length;

                    // обработаем команду
                    if (m.Groups[1].Value == "{")
                    {
                        // это переменная или выражение
                        _Template_Element el = new _Template_Element("V", templateElementCurrentNr);
                        el.expression = m.Groups[2].Value.Trim();
                        templateElements.Add(el);

                        templateElementCurrentNr++;
                    }
                    else
                    {
                        Match n_for = Regex.Match(m.Groups[2].Value, @"^\s*FOR\s+(\w+)\s+IN\s+(.+)$", RegexOptions.IgnoreCase);
                        Match n_breakif = Regex.Match(m.Groups[2].Value, @"^\s*BREAKIF\s+(.+)", RegexOptions.IgnoreCase);
                        Match n_efor = Regex.Match(m.Groups[2].Value, @"^\s*ENDFOR\s*$", RegexOptions.IgnoreCase);
                        Match n_if = Regex.Match(m.Groups[2].Value, @"^\s*IF\s+(.+)$", RegexOptions.IgnoreCase);
                        Match n_else = Regex.Match(m.Groups[2].Value, @"^\s*ELSE\s*$", RegexOptions.IgnoreCase);
                        Match n_eif = Regex.Match(m.Groups[2].Value, @"^\s*ENDIF\s*$", RegexOptions.IgnoreCase);

                        // TODO: Здесь разбираем команды поблочно
                        if (n_for.Success)
                        {
                            // FOR
                            _Template_Element el = new _Template_Element("F", templateElementCurrentNr);
                            el.value = n_for.Groups[1].Value.Trim();
                            el.expression = n_for.Groups[2].Value.Trim();
                            templateElements.Add(el);
                            stack_for.Push(templateElementCurrentNr);

                            templateElementCurrentNr++;
                        }
                        else if (n_breakif.Success)
                        {
                            // BREAKIF
                            _Template_Element el = new _Template_Element("BI", templateElementCurrentNr);
                            el.expression = n_breakif.Groups[1].Value.Trim();
                            templateElements.Add(el);
                            if (stack_for.Count == 0)
                            {
                                // ошибка. BREAKIF без родительского FOR
                                throw new Exception($"<-- ERROR: 'BREAKIF' WITHOUT 'FOR' -->");
                            }
                            el.parent = (int)stack_for.Last(); // parent FOR

                            templateElementCurrentNr++;
                        }
                        else if (n_efor.Success)
                        {
                            // ENDFOR
                            _Template_Element el = new _Template_Element("EF", templateElementCurrentNr);
                            if (stack_for.Count == 0)
                            {
                                // ошибка. Не совпадает количество FOR и ENDFOR
                                throw new Exception($"<-- ERROR: NUMBER OF 'FOR' AND 'ENDFOR' STATEMENTS DO NOT MATCH -->");
                            }
                            el.parent = stack_for.Pop();
                            templateElements.Add(el);
                            templateElements[(int)el.parent].end = templateElementCurrentNr; // FOR.END = current block

                            templateElementCurrentNr++;
                        }
                        else if (n_if.Success)
                        {
                            // IF
                            _Template_Element el = new _Template_Element("I", templateElementCurrentNr);
                            el.expression = n_if.Groups[1].Value.Trim();
                            templateElements.Add(el);
                            stack_if.Push(templateElementCurrentNr);

                            templateElementCurrentNr++;
                        }
                        else if (n_else.Success)
                        {
                            // ELSE
                            _Template_Element el = new _Template_Element("EL", templateElementCurrentNr);
                            if (stack_if.Count == 0)
                            {
                                // ошибка. Нету начального IF
                                throw new Exception($"<-- ERROR: 'ELSE' STATEMENT WITHOUT 'IF' -->");
                            }
                            templateElements[(int)stack_if.Last()].aux1 = templateElementCurrentNr; // IF.ELSE = current block
                            templateElements.Add(el);

                            templateElementCurrentNr++;
                        }
                        else if (n_eif.Success)
                        {
                            // ENDIF
                            _Template_Element el = new _Template_Element("EI", templateElementCurrentNr);
                            if (stack_if.Count == 0)
                            {
                                // ошибка. Не совпадает количество IF и ENDIF
                                throw new Exception($"<-- ERROR: NUMBER OF 'IF' AND 'ENDIF' STATEMENTS DO NOT MATCH -->");
                            }
                            templateElements[(int)stack_if.Pop()].end = templateElementCurrentNr; // IF.END = current block
                            templateElements.Add(el);

                            templateElementCurrentNr++;
                        }
                    }

                }

                if (lastBlockStartPosition < template.Length)
                {
                    // завершающий кусок текста
                    _Template_Element el = new _Template_Element("T", templateElementCurrentNr);
                    el.expression = template.Substring(lastBlockStartPosition, template.Length - lastBlockStartPosition);
                    templateElements.Add(el);

                    templateElementCurrentNr++;
                }

                // если количество вызовов FOR или IF не равно количеству завершающих команд
                if (stack_for.Count != 0)
                {
                    // ошибка. Не совпадает количество FOR и ENDFOR
                    throw new Exception($"<-- ERROR: NUMBER OF 'FOR' AND 'ENDFOR' STATEMENTS DO NOT MATCH -->");
                }
                if (stack_if.Count != 0)
                {
                    // ошибка. Не совпадает количество IF и ENDIF
                    throw new Exception($"<-- ERROR: NUMBER OF 'IF' AND 'ENDIF' STATEMENTS DO NOT MATCH -->");
                }


                // --2-- Собираем шаблон обратно. ---------------------------------------------------------------
                string res = "";
                int cursor = 0;
                bool processBlock = true;
                TBaseStack if_stack = new TBaseStack();

                // TODO: Здесь обрабатываем блоки
                while (cursor < templateElements.Count)
                {
                    switch (templateElements[cursor].type)
                    {
                        case "T":
                            if (processBlock)
                            {
                                res += templateElements[cursor].expression;
                            }
                            cursor++;
                            break;

                        case "V":
                            if (processBlock)
                            {
                                res += Convert.ToString(EvaluateExpression((string)templateElements[cursor].expression));
                            }
                            cursor++;
                            break;

                        case "F":
                            if (processBlock)
                            {
                                if (templateElements[cursor].idx < 0)
                                {
                                    // требуется реинициализация энумератора FOR - вычисляем значения генератора
                                    Match range = Regex.Match((string)templateElements[cursor].expression, @"^range\((.+?),(.+?)(,(.*))?\)$", RegexOptions.IgnoreCase);
                                    if (range.Success)
                                    {
                                        templateElements[cursor].aux1 = new RangeGenerator(EvaluateExpression(range.Groups[1].Value),
                                                                            EvaluateExpression(range.Groups[2].Value),
                                                                            (range.Groups[4].Success ? EvaluateExpression(range.Groups[4].Value) : 1));
                                    }
                                    else
                                    {
                                        //dynamic v = getDictionaryValue((string)templateElements[cursor].expression);
                                        dynamic v = parseValue((string)templateElements[cursor].expression);
                                        if (v == null)
                                        {
                                            // ошибка. Неизвестная переменная
                                            throw new Exception($"<-- ERROR: UNKNOWN VALUE IN FOR '{templateElements[cursor].expression}' -->");
                                        }
                                        else
                                        {
                                            templateElements[cursor].aux1 = (System.Collections.IEnumerator)v.GetEnumerator();
                                        }
                                    }
                                }

                                System.Collections.IEnumerator e = (System.Collections.IEnumerator)templateElements[cursor].aux1;
                                
                                if (e.MoveNext())
                                {
                                    templateElements[cursor].idx++;
                                    this.intVals[(string)templateElements[cursor].value] = e.Current;
                                }
                                else
                                {
                                    //e.Reset();
                                    templateElements[cursor].idx = -1;
                                    cursor = (int)templateElements[cursor].end;
                                }
                            }
                            cursor++;
                            break;

                        case "BI":
                            if (processBlock)
                            {
                                bool isBreak = (bool)EvaluateExpression((string)templateElements[cursor].expression);
                                if (isBreak)
                                {
                                    templateElements[(int)templateElements[cursor].parent].idx = -1;
                                    cursor = (int)templateElements[(int)templateElements[cursor].parent].end;
                                }
                            }
                            cursor++;
                            break;

                        case "EF":
                            if (processBlock)
                            {
                                cursor = (int)templateElements[cursor].parent;
                            }
                            break;

                        case "I":
                            if (processBlock)
                            {
                                if_stack.Push(processBlock);
                                processBlock = (bool)EvaluateExpression((string)templateElements[cursor].expression);
                            }
                            cursor++;
                            break;

                        case "EL":
                            processBlock = !processBlock;
                            cursor++;
                            break;

                        case "EI":
                            processBlock = (bool)if_stack.Pop();
                            cursor++;
                            break;

                        default:
                            cursor++;
                            break;
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Конвертирует строку из одной кодировки в другую.
        /// </summary>
        /// <param name="text">Строка для перекодирования.</param>
        /// <param name="fromEncodingStr">Кодировка исходной строки.</param>
        /// <param name="toEncodingStr">Требуемая кодировка.</param>
        /// <returns>Перекодированная строка.</returns>
        public static string ConvertString(string text, string fromEncodingStr, string toEncodingStr)
        {
            try
            {
                Encoding fromEnc = System.Text.Encoding.GetEncoding(fromEncodingStr);
                Encoding toEnc = System.Text.Encoding.GetEncoding(toEncodingStr);
                byte[] bytes = fromEnc.GetBytes(text);
                return toEnc.GetString(bytes);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Генератор для функции RANGE(from, to, step)
        /// </summary>
        public class RangeGenerator : IEnumerator
        {
            private dynamic current;
            private dynamic start;
            private dynamic stop;
            private dynamic step;


            public RangeGenerator(dynamic start, dynamic stop, dynamic step)
            {
                this.current = start - step;
                this.start = start;
                this.stop = stop;
                this.step = step;
            }

            public void Dispose() { }

            public object Current { get { return this.current; } }
            
            
            public bool MoveNext()
            {
                this.current += this.step;
                if (this.current < this.stop)
                    return true;
                else
                    return false;
            }

            public void Reset() { this.current = this.start - this.step; }
        }

        //public IEnumerable<int> Generator(int from, int to, int step)
        //{
        //    for (int i=from; i<to; i+=step)
        //    {
        //        yield return i;
        //    }
        //}


        /// <summary>
        /// Получить значение переменной из словаря. 
        /// Вначале всегда ищется во внутреннем словаре.
        /// </summary>
        /// <param name="key">Имя переменной</param>
        /// <param name="onlyCheck">Если true - то производится только проверка, есть ли переменная в словарях.
        /// По умолчанию = false.</param>
        /// <returns>Если onlyCheck=false, то возвращается значение из словаря. Исключение если не найдено.
        /// Если onlyCheck=true, то возвращает true, если значение найдено в словарях, иначе false.</returns>
        private dynamic getDictionaryValue(string key, bool onlyCheck=false)
        {
            dynamic x = null;
            bool valExists = false;

            if (this.intVals.ContainsKey(key))
            {
                x = this.intVals[key];
                valExists = true;
            }
            else
            {
                if (this.extVals != null && this.extVals.ContainsKey(key))
                {
                    x = this.extVals[key];
                    valExists = true;
                }
                else
                {
                    valExists = false;
                }
            }

            if (onlyCheck)
            {
                return valExists;
            }
            else
            {
                if (!valExists)
                {
                    // ошибка. Неизвестная переменная
                    throw new Exception($"<-- ERROR: UNKNOWN VALUE '{key}' -->");
                }
            }

            return x;
        }


        /// <summary>
        /// Преобразует строку в инфиксной записи в строку с постфиксной записью (обратная польская записЬ).
        /// Поддерживаются переменные (переменная - любое слово начинающееся с символа $).
        /// Так же поддерживаются строки (начинаются с одинарной кавычки.
        /// Используется алгоритм сортировочной станции
        /// </summary>
        /// <param name="exp">Строка в инфиксной записи</param>
        /// <returns>Массив строк в постфиксной записи. Null - если формула записана с ошибками.</returns>
        private string[] convertExpressionToPostfix(ref string infixExpression)
        {
            List<string> stack = new List<string>();
            List<string> outbound = new List<string>();

            // разделим элементы (символы разделения добавляются спереди или сзади знаков ^*/+-()
            string separators = operators.opsList() + @"(?=([^']*'[^']*')*[^']*$)";
            string expression = Regex.Replace(infixExpression, separators, "\0$0\0");
            string[] inbound_temp = expression.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> inbound = new List<string>();

            // небольшая субфункция для проверки последнего символа предыдущей непустой строки
            // возвращает true, если последний символ - цифра или переменная
            bool needBeSubstracted(int index)
            {
                if (index == 0) return true;
                for (int i=index-1; i>=0; i--)
                {
                    string t = inbound_temp[i].Trim();
                    if (t != "")
                    {
                        if (char.IsLetterOrDigit(t.Last()) || t.Last() == ')')
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                return true;
            }

            // ищет следующий непустой элемент
            int nextElement(int index)
            {
                for (int i=index + 1; i<inbound_temp.Length; i++)
                {
                    string t = inbound_temp[i].Trim();
                    if (t != "") return i;
                }
                throw new Exception();
            }
            
            // проверка на отрицательные числа и пустые строки
            for (int i = 0; i < inbound_temp.Length; i++)
            {
                if (inbound_temp[i] == "-")
                {
                    // минус прямо в начале
                    if (needBeSubstracted(i))
                    {
                        int nextElementHere = nextElement(i);
                        inbound.AddRange(new string[] { "(", "0", "-", inbound_temp[nextElementHere], ")" });
                        i = nextElementHere;
                    }
                    else
                    {
                        inbound.Add(inbound_temp[i]);
                    }
                }
                else
                {
                    if (inbound_temp[i].Trim() != string.Empty) inbound.Add(inbound_temp[i]);
                }
            }

            // обрабатываем
            foreach (string s in inbound)
            {
                string v = s.Trim();
                if (Regex.IsMatch(v, @"^[\d.]+"))
                {
                    // число - сразу в выходную очередь
                    outbound.Add(v);
                }
                else if (Regex.IsMatch(v, @"^'.*'$"))
                {
                    // текст - в выходную очередь
                    outbound.Add(v);
                }
                else if (v == "(")
                {
                    // открывающая скобка
                    stack.Add(s);
                }
                else if (v == ")")
                {
                    // закрывающая скобка
                    if (stack.Count > 0)
                        while (stack.Last() != "(")
                        {
                            outbound.Add(stack.Last());
                            stack.RemoveAt(stack.Count - 1);
                        }
                    else
                        throw new Exception();

                    if (stack.Count > 0)
                        stack.RemoveAt(stack.Count - 1);
                    else
                        // должна быть открывающая скобка
                        throw new Exception();
                }
                else if (v == "true" || v == "false")
                {
                    // логическая константа
                    outbound.Add(v);
                }
                else if (Regex.IsMatch(v, operators.opsList()))
                { 
                    // оператор
                    if (stack.Count > 0)
                        while (operators.CompareIfFirstGreaterOrEqual(stack.Last(), v))
                        {
                            outbound.Add(stack.Last());
                            stack.RemoveAt(stack.Count - 1);
                            if (stack.Count == 0) break;
                        }
                    stack.Add(v);
                }
                else 
                {
                    // все что осталось полагаем переменная - вычислим и в выходную очередь
                    try
                    {
                        dynamic x = this.parseValue(v);
                        if (x is System.String)
                        {
                            outbound.Add($"'{x}'");
                        }
                        else if (x is bool)
                        {
                            outbound.Add(x.ToString().ToLower());
                        }
                        else
                        {
                            outbound.Add(Convert.ToString(x));
                        }
                    }
                    catch
                    {
                        // ошибка. Неизвестное выражение
                        throw new Exception($"<-- ERROR: UNKNOWN EXPRESSSION '{s}' -->");
                    }
                }
            }

            // оставшиеся операторы
            while (stack.Count > 0)
            {
                // не должны остаться скобки в формуле
                if (Regex.IsMatch(outbound.Last(), "^[()].*"))
                    throw new Exception();

                outbound.Add(stack.Last());
                stack.RemoveAt(stack.Count - 1);
            }

            return outbound.ToArray();
        }


        /// <summary>
        /// Обработчик имени переменной.
        /// Переменная может быть многоуровневой, например "data1.field3.field4[1].field1"
        /// Поддержка 1D массивов любого уровня вложенности. Например "arr[1]" или "arr[1][2]..[n]"
        /// В случае ошибки генерируется исключение с соответствующим текстом.
        /// </summary>
        /// <param name="data">Ссылка на строку содержающую переменную</param>
        /// <returns>Возвращает значение переменной преобразованное к строке.</returns>
        dynamic parseValue(string data)
        {
            List<object> res = new List<object>();
            dynamic d = null;
            bool firstAttribute = true;

            string[] fields = data.Split('.');

            foreach (string field in fields)
            {
                string f = field;
                var r = findPattern(ref f, 0, "[", "]");
                if (r.lastSearchIndex < 0)
                {
                    // это аттрибут
                    if (firstAttribute)
                    {
                        if (!this.getDictionaryValue(field, true))
                        {
                            // ошибка. Нет такой переменной в словаре
                            throw new Exception($"<-- ERROR: VALUE '{field}' IS NOT DEFINED -->");
                        }
                        d = this.getDictionaryValue(field);
                        firstAttribute = false;
                    }
                    else
                    {
                        // проверим это поле или свойство
                        if (d.GetType().GetField(field) != null)
                        {
                            try
                            {
                                d = d.GetType().GetField(field).GetValue(d); // объект атрибута по имени
                            }
                            catch
                            {
                                // ошибка. Нет такого поля
                                throw new Exception($"<-- ERROR: FIELD '{field}' OF VALUE '{r.data}' IS NOT DEFINED -->");
                            }
                        }
                        else if (d.GetType().GetProperty(field) != null)
                        {
                            try
                            {
                                d = d.GetType().GetProperty(field).GetValue(d); // объект атрибута по имени
                            }
                            catch
                            {
                                // ошибка. Нет такого свойства
                                throw new Exception($"<-- ERROR: PROPERTY '{field}' OF VALUE '{r.data}' IS NOT DEFINED -->");
                            }
                        }
                        else
                        {
                            // ошибка. Возможно это метод
                            throw new Exception($"<-- ERROR: ELEMENT '{field}' OF VALUE '{r.data}' IS UNKNOWN -->");
                        }
                    }
                }
                else
                {
                    // это массив
                    string arrayName = field.Substring(0, r.firstSearchIndex);

                    if (firstAttribute)
                    {
                        if (!this.getDictionaryValue(arrayName, true))
                        {
                            // ошибка. Нет такой переменной в словаре
                            throw new Exception($"<-- ERROR: VALUE '{arrayName}' IS NOT DEFINED -->");
                        }
                        d = this.getDictionaryValue(arrayName);
                        firstAttribute = false;
                    }
                    else
                    {
                        try
                        {
                            d = d.GetType().GetField(arrayName).GetValue(d); // объект атрибута по имени
                        }
                        catch
                        {
                            // ошибка. Нет такого поля
                            throw new Exception($"<-- ERROR: FIELD '{arrayName}' of value '{r.data}' IS NOT DEFINED -->");
                        }
                    }

                    if (d.GetType().IsGenericType && d.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)))
                    {
                        // это словарь
                        string key = Regex.Replace(r.data, @"['""]", "");
                        d = d[key];
                    }
                    else
                    {
                        // это объект по типу массива
                        do
                        {
                            string evaluatedIndex = EvaluateExpression(r.data).ToString();
                            int arrayIndex;
                            if (int.TryParse(evaluatedIndex, out arrayIndex))
                            {
                                try
                                {
                                    d = d[arrayIndex];
                                }
                                catch
                                {
                                    // ошибка. Индекс массива за пределами диапазона
                                    throw new Exception($"<-- ERROR: ARRAY '{r.data}' - INDEX OUT OF RANGE -->");
                                }
                            }
                            else
                            {
                                // Значение в квадратных скобках не числовое. Пробуем по имени
                                try
                                {
                                    d = d[evaluatedIndex];
                                }
                                catch
                                {
                                    // ошибка. Значение в квадратных скобках не индекс массива
                                    throw new Exception($"<-- ERROR: UNKNOWN INDEXER '{r.data}' FOR ARRAY -->");
                                }
                            }

                            r = findPattern(ref f, r.lastSearchIndex, "[", "]");
                        } while (r.lastSearchIndex >= 0);
                    }

                }
            }

            if (d == null) d = string.Empty;
            return d;// Convert.ToString(d);
        }


        /// <summary>
        /// Структура (класс) поиска по паттерну
        /// </summary>
        class TSearchInfo
        {
            public int firstSearchIndex;
            public int lastSearchIndex;
            public string data;
            public TSearchInfo()
            {
                firstSearchIndex = lastSearchIndex = -1;
                data = "";
            }
        }


        /// <summary>
        /// Находит подстроку ограниченную указанными символами 
        /// </summary>
        /// <param name="T">Строка в которой ищем.</param>
        /// <param name="fromPos">С какой позиции ищем</param>
        /// <param name="patternStart">Строка начала паттерна</param>
        /// <param name="patternEnd">Строка окончания паттерна</param>
        /// <returns>Структура TSearchInfo</returns>
        TSearchInfo findPattern(ref string T, int fromPos, string patternStart, string patternEnd)
        {
            TSearchInfo res = new TSearchInfo();
            res.firstSearchIndex = T.IndexOf(patternStart, fromPos);
            if (res.firstSearchIndex >= 0)
            {
                res.lastSearchIndex = T.IndexOf(patternEnd, res.firstSearchIndex + patternStart.Length);
                if (res.lastSearchIndex >= 0)
                {
                    res.data = T.Substring(res.firstSearchIndex + patternStart.Length,
                                           res.lastSearchIndex - (res.firstSearchIndex + patternStart.Length)).Trim();
                    res.lastSearchIndex += patternEnd.Length;
                }
            }

            return res;
        }


        /// <summary>
        /// Вычисляет значение выражения. 
        /// </summary>
        /// <param name="infixExpression">Строка с выражением.</param>
        /// <returns>Результат выражения в виде строки или Exception, если ошибка.</returns>
        public object EvaluateExpression(string infixExpression)
        {
            // проверим не является ли строка операцией с переменными вида x=...
            // в этом случае сделаем присвоение и ничего не вернем
            if (Regex.IsMatch(infixExpression, @"^[a-zA-Z]+\w*\s*=(?!=).+$"))
            {
                string[] v = infixExpression.Split('=');
                object x = this.EvaluateExpression(v[1].Trim());
                this.intVals[v[0].Trim()] = x;
                return "";
            }

            TStack stack = new TStack();

            // преобразование в потсфикс
            string[] exp = convertExpressionToPostfix(ref infixExpression);

            // вычисление
            if (exp == null)
            {
                // ошибка. нечего парсить
                throw new Exception($"<-- ERROR: UNKNOWN ERROR IN EXPRESSION -->");
            }

            foreach (string s in exp)
            {
                if (operators.Exists(s))
                {
                    // оператор
                    stack.Eval(s);
                }
                else if (Regex.IsMatch(s, @"^'.*'$"))
                {
                    // строка в одинарных или двойных кавычках
                    stack.Push(s.Substring(1, s.Length - 2));
                }
                else if (s == "true" || s == "false")
                {
                    stack.Push(bool.Parse(s));
                }
                else
                {
                    // полагаем число
                    try
                    {
                        stack.Push(Convert.ToDouble(s.Replace('.', ',')));
                    }
                    catch
                    {
                        // ошибка. нечего парсить
                        throw new Exception($"<-- ERROR: THE VALUE '{s}' MUST BE NUMERIC -->");
                    }
                }
            }

            return stack.Pop();
        }


    }


    /// <summary>
    /// Класс элемента шаблона.
    /// Index - порядковый номер блока
    /// Type - Тип:
    /// T - текст. expression - содержит текст который выводится как есть.
    /// V - переменная или выражение. expression - содержит текстовое выражение которое должно быть вычислено.
    /// F - заголовок цикла FOR. expression - текстовое имя переменной внутреннего цикла,
    ///                          aux1 - вначале выражение, потом объект IEnumerable
    ///                          end - индекс ENDFOR
    /// BI - прервать цикл по условию. индекс родительского TemplateElement
    /// EF - окончание цикла FOR. parent - индекс родительского TemplateElement
    /// I - заголовок условия IF. expression - логическое выражение.
    ///                           aux1 - индекс элемента ELSE
    ///                           end - индекс элемента ENDIF
    /// EL - иное значение ELSE.  end - индекс элемента ENDIF                          
    /// EI - окончание условия IF. 
    /// </summary>
    class _Template_Element
    {
        public string type; // тип блока (T, V, F, EF, I, EL, EI)
        public int index; // индекс блока в template_elements
        public string value; // имя переменной (только для цикла)
        public string expression; // вычисляемое выражение или тело блока
        public object parent; // индекс блока родительского элемента (для ENDxx)
        public object end; // индекс блока элемента ENDxx
        public int idx; // текущий индекс (только для FOR): -1=цикл закончился или не начинался. 0,1,2...
        public object aux1; // вспомогательный объект



        public _Template_Element(string type, int index)
        {
            this.type = type;
            this.index = index;
            this.expression = this.value = "";
            this.parent = this.end = this.aux1 = null;
            this.idx = -1;
        }
    }


    class TBaseStack
    {
        /// <summary>
        /// Конструктор
        /// </summary>
        public TBaseStack()
        {
            stack = new List<object>();
        }

        /// <summary>
        /// Стек операндов и операторов
        /// </summary>
        public List<object> stack;

        /// <summary>
        /// Положить значение в стек
        /// </summary>
        public void Push(object val)
        {
            stack.Add(val);
        }

        /// <summary>
        /// Достать значение из стека
        /// </summary>
        /// <returns>Если в стеке нет значений возвращает null</returns>
        public object Pop()
        {
            if (stack.Count > 0)
            {
                object x = stack.Last();
                stack.RemoveAt(stack.Count - 1);
                return x;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Возвращает последний элемент стека (последний добавленный).
        /// </summary>
        /// <returns>Если в стеке нет значений возвращает null</returns>
        public object Last()
        {
            if (stack.Count > 0)
            {
                object x = stack.Last();
                return x;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Возвращает количество элементов в стеке
        /// </summary>
        public int Count
        {
            get
            {
                return stack.Count;
            }
        }
    }

    /// <summary>
    /// Стек для вычисления обратной польской записи с поддержкой скобок.
    /// Умеет работать с числами и строками.
    /// Числа всегда double. Для них могут выполнятся следующие действия: ^ * / + -
    /// Для строк выполняется только сложение как конкатенация. Допускается сложение строк и чисел (как строк).
    /// </summary>
    class TStack : Stack<object>
    {
        /// <summary>
        /// Проверяет, является ли значение числовым типом.
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private bool IsNumericType(object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Произвести унарную операцию с последними двумя числами в стеке.
        /// Операнды удаляются из стека. Результат кладется в стек.
        /// </summary>
        /// <param name="op">Операция</param>
        /// <returns>True - если операция выполнена. Исключение - неизвестный оператор.</returns>
        public void Eval(string op)
        {
            int err = 0;
            object x2 = this.Pop();
            object x1 = this.Pop();
            object x = null;

            if (x1 == null)
                if (x2 is System.String)
                    x1 = string.Empty;
                else
                    x1 = (double)0.0f;

            switch (op)
            {
                case "+":
                    if (x1 is System.String || x2 is System.String)
                        x = String.Concat(x1, x2);
                    else if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 + (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "-":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 - (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "*":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 * (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "/":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        if ((dynamic)x2 == 0)
                        {
                            err = 3;
                        }
                        else
                        {
                            x = (dynamic)x1 / (dynamic)x2;
                        }
                        
                    }
                    else
                    {
                        err = 2;
                    }
                    break;


                case "%":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        if ((dynamic)x2 == 0)
                        {
                            err = 3;
                        }
                        else
                        {
                            x = (dynamic)x1 % (dynamic)x2;
                        }

                    }
                    else
                    {
                        err = 2;
                    }
                    break;


                case "^":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = Math.Pow((dynamic)x1, (dynamic)x2);
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "==":
                    if (x1.GetType() == x2.GetType())
                    {
                        x = x1.Equals(x2);
                    }
                    else
                    {
                        try
                        {
                            x = x1 == x2;
                        }
                        catch
                        {
                            err = 2;
                        }
                    }
                    break;

                case "!=":
                    if (x1.GetType() == x2.GetType())
                    {
                        x = !x1.Equals(x2);
                    }
                    else
                    {
                        try
                        {
                            x = x1 != x2;
                        }
                        catch
                        {
                            err = 2;
                        }
                    }
                    break;

                case "<":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 < (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "<=":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 <= (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case ">":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 > (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case ">=":
                    if (IsNumericType(x1) && IsNumericType(x2))
                    {
                        x = (dynamic)x1 >= (dynamic)x2;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "!":
                    if (x2 is bool)
                    {
                        x = !(bool)x2;
                        if (x1 != null) this.Push(x1);
                    }
                    else
                    {
                        err = 4;
                    }
                    break;

                case "&&":
                    if (x1 is bool && x2 is bool)
                    {
                        x = (bool)x2 && (bool)x1;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                case "||":
                    if (x1 is bool && x2 is bool)
                    {
                        x = (bool)x2 || (bool)x1;
                    }
                    else
                    {
                        err = 2;
                    }
                    break;

                default:
                    err = 1;
                    break;
            }

            if (err != 0)
            {
                this.Push(x1);
                this.Push(x2);

                switch (err)
                {
                    case 1:
                        // ошибка. Неизвестный оператор
                        throw new Exception($"<-- ERROR: UNKNOWN OPERATOR '{op}' -->");
                    case 2:
                        // ошибка. Несовместимый с данными операндами оператор
                        throw new Exception($"<-- ERROR: INCOMPATIBLE OPERAND '{x1}' AND '{x2}' FOR OPERATOR '{op}' -->");
                    case 3:
                        // ошибка. Несовместимый с данными операндами оператор
                        throw new Exception($"<-- ERROR: DIVISION BY ZERO '{x1}' / '{x2}' -->");
                    case 4:
                        // ошибка. Несовместимый с данными операндами оператор
                        throw new Exception($"<-- ERROR: OPERATOR '{op}' IS INCOMPATIBLE WITH OPERAND '{x2}' -->");
                }
                
            }
            else
            {
                this.Push(x);
            }
        }
    }



    /// <summary>
    /// Класс содержащий операторы и их приоритеты. Содержит методы по извлечению и сравнению приоритетов.
    /// </summary>
    static class operators
    {
        private static Dictionary<string, int> ops = new Dictionary<string, int>()
            {
                { "^",  7 },
                { "*",  6 },
                { "/",  6 },
                { "%",  6 },
                { "+",  5 },
                { "-",  5 },
                { "!",  4 },
                { "<",  3 },
                { ">",  3 },
                { "<=", 3 },
                { ">=", 3 },
                { "==", 2 },
                { "!=", 2 },                
                { "||", 1 },
                { "&&", 1 },
                { "(",  0 },
                { ")",  0 },
            };

        /// <summary>
        /// Возвращает приоритет оператора.
        /// </summary>
        /// <param name="op"></param>
        /// <returns>Целое число начиная с 0. Генерирует исключение, если оператор не найден</returns>
        public static int GetPriority(string op)
        {
            if (ops.ContainsKey(op))
            {
                return ops[op];
            }
            else
            {
                throw new Exception($"<-- ERROR: UNKNOWN OPERATOR '{op}' -->");
            }
        }

        /// <summary>
        /// Сравнивает два оператора.
        /// </summary>
        /// <param name="op1"></param>
        /// <param name="op2"></param>
        /// <returns>Возвращает true, если op1 >= op2</returns>
        public static bool CompareIfFirstGreaterOrEqual(string op1, string op2)
        {
            int priority_op1 = GetPriority(op1);
            int priority_op2 = GetPriority(op2);
            return priority_op1 >= priority_op2;
        }

        /// <summary>
        /// Возвращает список операторов в формате "\<op>|\<op>..." для использования в Regex.
        /// Список собирается таким образом, что бы слева были длинные операторы.
        /// </summary>
        /// <returns></returns>
        public static string opsList()
        {
            string ret = "";

            int max = 0;
            foreach (string key in ops.Keys)
                if (key.Length > max) max = key.Length;

            for (int i=max; i>0; i--)
            {
                foreach (string key in ops.Keys)
                    ret += (key.Length == i ?
                            $"{Regex.Replace(key, @"[\^\*\/\+\-\?\(\)\|\&]", "\\$0")}|"
                            : "");
            }

            // экранируем управляющие символы Regex
            return ret.Substring(0, ret.Length - 1);
        }

        /// <summary>
        /// Показывает содержится ли оператор в списке. В отличие от GetPriority не выбрасывает исключение, если оператор не существует
        /// </summary>
        /// <param name="op"></param>
        /// <returns>true - если оператор есть в списке, инача false</returns>
        public static bool Exists(string op)
        {
            return ops.ContainsKey(op);
        }
    }


    /// <summary>
    /// Вспомогательный класс для обработки запросов от HttpListener
    /// </summary>
    public class TRequest
    {
        public TRequest(HttpListenerRequest request)
        {
            this.values = new Dictionary<string, object>();
            switch (request.HttpMethod)
            {
                case "POST":
                    this.Method = RequestTypes.POST;
                    break;
                case "GET":
                    this.Method = RequestTypes.GET;
                    break;
                default:
                    this.Method = RequestTypes.UNKNOWN;
                    break;
            }
            this.encoding = request.ContentEncoding;
            this.Route = request.RawUrl.Split('?')[0];

            if (this.Method == RequestTypes.GET)
            {
                foreach (string key in request.QueryString.AllKeys)
                {
                    this.values.Add(key, request.QueryString[key]);
                }
            }
            else if (this.Method == RequestTypes.POST)
            {
                //"application/x-www-form-urlencoded"
                System.IO.StreamReader reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                string s = reader.ReadToEnd();
                if (s != "")
                {
                    string[] values = s.Split('&');
                    foreach (string value in values)
                    {
                        string[] pair = value.Split('=');
                        this.values.Add(pair[0], HttpUtility.UrlDecode(pair[1]));
                    }
                }
                request.InputStream.Close();
                reader.Close();
            }
        }

        public RequestTypes Method;
        public string Route;
        public Encoding encoding;
        public Dictionary<string, object> values;

        public enum RequestTypes : int
        {
            POST,
            GET,
            UNKNOWN
        }
    }


    /// <summary>
    /// Вспомогательный класс для обработки JSON объектов
    /// </summary>
    public static class TJson
    {
        public static string ConvertToJson(object o)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(o);
        }

        public static string quoted(string text) => $"\"{text}\"";
    }
}
