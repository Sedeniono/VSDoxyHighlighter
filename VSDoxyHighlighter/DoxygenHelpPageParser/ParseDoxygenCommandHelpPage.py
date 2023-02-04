# Script that parses the Doxygen command help page
# https://www.doxygen.nl/manual/commands.html,
# extracts the documentation for each command and
# generates a C# file that exposes the information.

import bs4
import os
from datetime import datetime
import re


class ParsedCommand:
    def __init__(self, header: str, help_text: str):
        assert(len(header) > 0)
        self.raw_header = escape_characters(header)
        self.help_text = escape_characters(help_text)

        if header[0] != "\\":
            raise Exception(f"Header does not start with '\\': {header}")
        
        (self.command, self.parameters) = split_command_header(header)
        self.command = escape_characters(self.command)
        self.parameters = escape_characters(self.parameters)


def escape_characters(raw_string: str):
    return raw_string.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "").replace("\n", "\\n")


def split_command_header(header: str):
    # Split the header into command and parameter. For example:
    #    \example['{lineno}'] <file-name>
    #    \file [<name>]
    space_pos = header.find(" ")
    if space_pos == -1:
        space_pos = len(header)

    square_bracket_pos = header.find("[")
    if square_bracket_pos == -1:
        square_bracket_pos = len(header)

    if square_bracket_pos < space_pos:
        cmd_end_pos = square_bracket_pos
        param_start_pos = square_bracket_pos
    else:
        cmd_end_pos = space_pos
        param_start_pos = min(space_pos + 1, len(header))

    command = header[1:cmd_end_pos]
    parameters = header[param_start_pos:]
    return (command, parameters)



def parse_doxygen_help_html(file) -> list[ParsedCommand]:
    soup = bs4.BeautifulSoup(file, 'html.parser')
    
    all_parsed_commands: list[ParsedCommand] = []

    # The actual descriptions start at the first <h1> tag after the first <center> tag.
    tag = soup.find("center")
    tag = tag.find_next_sibling("h1")

    while tag != None:
        header_tag = tag
        tag = tag.next_sibling
        description_tags = []

        # The description of one Doxygen command continues until the next <h1> tag.
        while tag != None and tag.name != "h1":
            description_tags.append(tag)
            tag = tag.next_sibling
        
        parsed = parse_html_tags_of_single_command(header_tag, description_tags)
        all_parsed_commands.append(parsed)

    return all_parsed_commands
        

def parse_recursive(tag: bs4.element.PageElement, decorator) -> str:
    if isinstance(tag, bs4.element.Comment):
        return ""

    elif isinstance(tag, str):
        return decorator(tag)

    elif tag.name == "p":
        s = parse_all_children(tag.children, lambda x: decorator(x).replace("\r", "").replace("\n", "")).strip(" ")
        if s == "Click here  for the corresponding HTML documentation that is generated by doxygen.":
            return ""
        elif s == "Go to the next section or return to the index.":
            return ""
        if s != "" and tag.next_sibling.name != "ul":
            s += "\n"
        return s

    elif tag.name == "code":
        return parse_all_children(tag.children, decorator)

    elif tag.name == "em":
        return "**" + parse_all_children(tag.children, decorator) + "**"

    elif tag.name == "dl" and ' '.join(tag['class']) == "section see":
        if len(tag.contents) != 2:
            raise Exception("Expected the 'section see' to always have exactly 2 children.")
        see_also_content = parse_all_children(tag.contents[1:], decorator)
        # The documentation often has something like "See also: Section \page for an example"
        # which reads weird in the Visual Studio tooltip, since one cannot click there. So 
        # replace "section" with "command". But not the \section command itself.
        see_also_content = re.sub(r"\b(?<!\\)section\b", "command", see_also_content)
        see_also_content = re.sub(r"\b(?<!\\)Section\b", "Command", see_also_content)
        see_also_content = re.sub(r"\b(?<!\\)sections\b", "commands", see_also_content)
        see_also_content = re.sub(r"\b(?<!\\)Sections\b", "Commands", see_also_content)
        return "See also: " + see_also_content.strip() + "\n"

    elif tag.name == "dl" and ' '.join(tag['class']) in ["section note", "section warning"]:
        tag_class = ' '.join(tag['class'])
        s = ""
        if tag.previous_sibling != "\n":
            s += "\n"
        if "note" in tag_class:
            s += "Note:"
        else:
            s += "Warning:"
        if len(tag.contents) > 2:
            for child in filter(lambda x: x != "\n", tag.contents[1:]):
                s += "\n\t" + parse_all_children([child], decorator).strip()
        else:
            s += " " + parse_all_children(tag.contents[1:], decorator).strip()
        s += "\n"
        return s

    elif tag.name == "dl" and ' '.join(tag['class']) == "section user":
        # Either some example code, or some note
        return parse_all_children(tag.children, decorator) + "\n"

    elif tag.name == "dt":
        return parse_all_children(tag.children, decorator) + " "

    elif tag.name == "pre" or (tag.name == "div" and ' '.join(tag['class']) == "fragment") or tag.name == "blockquote":
        # Some code
        s = tag.get_text().strip("\n")
        lines = [("   " + l) for l in s.split("\n")]
        concat_lines = "\n".join(lines) + "\n\n"
        if tag.previous_sibling != "\n":
            concat_lines = "\n" + concat_lines
        return concat_lines

    elif tag.name == "ul":
        s = parse_all_children(tag.children, decorator)
        if len(s) > 0 and s[-1] == "\n":
            s = s.rstrip("\n") + "\n"
        return s

    elif tag.name == "li":
        stars = ""
        spaces = ""
        for parent in tag.parents:
            if parent.name == "ul":
                stars += "•"
                spaces += "  "
        s = spaces + stars + " " + parse_all_children(tag.children, decorator).lstrip()
        # Removes successive newlines between list elements. For example for the "\showdate" command.
        if s[-1] == "\n":
            s = s.rstrip("\n")
            if tag.next_sibling == None or tag.next_sibling != "\n":
                s += "\n"
        return s

    elif tag.name == "table":
        return parse_table(tag)

    elif tag.name == "img":
        if "LaTeX" in tag['alt']:
            return "LaTeX"
        else:
            return tag.get_text()
        
    elif tag.name == "center":
        # The "center" tag is only used for the "intermediate" headers like "Commands for displaying examples"
        # that separate the different command categories. We don't want them.
        return ""

    else:
        return parse_all_children(tag.children, decorator)


def parse_all_children(children, decorator) -> str:
    s = ""
    for child in children:
        s += parse_recursive(child, decorator)
    return s


def parse_table(table: bs4.element.Tag):
    table_contents = [x for x in filter(lambda x: x != "\n", table.contents)]
    if len(table_contents) != 1:
        raise Exception("Expected table to contain only a single tag")
    body_tag = table_contents[0]
    if body_tag.name != "tbody":
        raise Exception("Expected table to contain 'tbody'")
    
    t = body_tag.contents[0]
    rows = []
    while t != None:
        if t != "\n":
            columns = []
            for column_tag in t.children:
                if column_tag != "\n":
                    column_text = column_tag.get_text().strip()
                    columns.append(column_text)
            rows.append(columns)
        t = t.next_sibling

    column_widths = [0 for c in rows[0]]
    for row in rows:
        if len(row) != len(column_widths):
            raise Exception("Table has different number of columns in its rows")
        for column_index in range(0, len(row)):
            column_widths[column_index] = max(column_widths[column_index], len(row[column_index]))

    # Insert a separator between the header and the table content. The first row is assumed to be the header
    rows.insert(1, ["-" * column_widths[idx] for idx in range(0, len(column_widths))])

    header_row_prefix = "    "
    column_separator = "  "

    s = ""
    for row in rows:
        s += header_row_prefix
        for column_index in range(0, len(row)):
            s += row[column_index].ljust(column_widths[column_index], " ")
            if column_index != len(row)-1:
                s += column_separator
        s += "\n"
    return s


def parse_html_tags_of_single_command(header_tag, description_tags):
    description_text = ""

    for desc_tag in description_tags:
        s = parse_recursive(desc_tag, lambda x: x)

        s = s.replace("\n\n  \n  Click here\n  for the corresponding HTML documentation that is generated by doxygen.\n   ", "")
        s = s.replace("Click here  for the corresponding HTML documentation that is generated by doxygen.\n", "")

        description_text += s

    description_text = description_text.strip()
    header_text = header_tag.get_text().strip()
    return ParsedCommand(header_text, description_text)


def generate_text_for_csharp_file(commands: list[ParsedCommand]) -> str:
    python_file = os.path.basename(__file__)
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    max_command_len, max_parameters_len = get_max_component_lengths(commands)

    s = f"// This file was automatically generated by the Python script\n"
    s += f"// {python_file} on {current_time}.\n\n"

    s += "namespace VSDoxyHighlighter\n"
    s += "{\n"
    s += "  class DoxygenCommandsGeneratedFromHelpPage\n"
    s += "  {\n"
    s += "    public static readonly DoxygenHelpPageCommand[] cCommands = {\n"

    for cmd in commands:
        command_padding = " " * (max_command_len - len(cmd.command))
        parameters_padding = " " * (max_parameters_len - len(cmd.parameters))
        s += f'      new DoxygenHelpPageCommand("{cmd.command}",{command_padding} "{cmd.parameters}",{parameters_padding} "{cmd.help_text}"),\n'

    s += "    };\n"
    s += "  }\n"
    s += "}\n"

    return s


def generate_debug_dump(commands: list[ParsedCommand]) -> str:
    s = ""
    for cmd in commands:
        s += "====================================\n"
        s += f"Command: {cmd.command}\n"
        s += "Parameters: " + cmd.parameters.replace("\\\\", "\\").replace('\\"', '"') + "\n"
        s += "Help text:\n"
        s += cmd.help_text.replace("\\n", "\n").replace("\\\\", "\\").replace('\\"', '"')
        s += "\n<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n\n\n"
    return s


def get_max_component_lengths(commands: list[ParsedCommand]):
    command_len = 0
    parameters_len = 0
    for cmd in commands:
        command_len = max(command_len, len(cmd.command))
        parameters_len = max(parameters_len, len(cmd.parameters))
    return (command_len, parameters_len)


def extract_and_convert_doxygen_commands_from_html(html_filename: str, output_csharp_filename: str, output_debug_dump_filename: str):
    with open(html_filename, 'r', encoding='utf-8') as input_file:
        parsed_commands = parse_doxygen_help_html(input_file)
    
    csharp_text = generate_text_for_csharp_file(parsed_commands)
    with open(output_csharp_filename, 'w', encoding='utf-8') as output_file:
        output_file.write(csharp_text)

    dump_text = generate_debug_dump(parsed_commands)
    with open(output_debug_dump_filename, 'w', encoding='utf-8') as output_file:
        output_file.write(dump_text)


if __name__ == "__main__":
    extract_and_convert_doxygen_commands_from_html(
        "VSDoxyHighlighter/DoxygenHelpPageParser/testInput.htm", 
        "VSDoxyHighlighter/DoxygenHelpPageParser/DoxygenCommandsGeneratedFromHelpPage.cs",
        "VSDoxyHighlighter/DoxygenHelpPageParser/GeneratedDebugDump.txt")
