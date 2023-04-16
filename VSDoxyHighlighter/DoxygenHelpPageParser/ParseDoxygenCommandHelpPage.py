# Script that parses the Doxygen command help page
# https://www.doxygen.nl/manual/commands.html,
# extracts the documentation for each command and
# generates a C# file that exposes the information.

import bs4
import os
from datetime import datetime
import re
from enum import Enum
from typing import Union


class FragmentType(Enum):
    Text = 1
    Code = 2
    Emphasis = 3
    Note = 4
    Warning = 5
    Command = 6


class Fragment:
    """ Represents a piece of text from the Doxygen help text of a certain type.
        The whole help text is then a list of fragments."""
    def __init__(self, type: FragmentType, content: str, hyperlink: str = ""):
        self.type = type
        self.content = content
        self.hyperlink = hyperlink


class ParsedCommand:
    """ Contains the whole parsed information for a single Doxygen command.
        header: This should be the command and its parameters. I.e. the heading of the command description.
        anchor: The html anchor ID, used to link to the command.
        help_text: The full text below the heading.
    """
    def __init__(self, header: str, anchor: str, help_text: list[Fragment]):
        assert(len(header) > 0)
        self.raw_header = header
        self.help_text = help_text
        self.anchor = anchor

        if header[0] != "\\":
            raise Exception(f"Header does not start with '\\': {header}")
        (self.command, self.parameters) = split_command_header(header)

        # Variants with escaped characters that are suitable to be written into a C# source file.
        self.escaped_command = escape_characters(self.command)
        self.escaped_parameters = escape_characters(self.parameters)
        self.escaped_help_text = [Fragment(f.type, escape_characters(f.content), f.hyperlink) for f in self.help_text]


def escape_characters(raw_string: str):
    """ Escapes the given string such that it can be written to a C# source file."""
    # Note: No need to maintain the carriage return in the C# source file; the newline character is sufficient
    # for our purposes.
    return raw_string.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "").replace("\n", "\\n")


def split_command_header(header: str):
    """ Splits the header into command and parameter. For example:
         \example['{lineno}'] <file-name>   ==> Results in "\example" and "['{lineno}'] <file-name>"
         \file [<name>]                     ==> Results in "\file" and "[<name>]"
    """

    # Often, there is a space after the command that separates the argument.
    space_pos = header.find(" ")
    if space_pos == -1:
        space_pos = len(header)

    # However, in some cases the space is missing. In all of these cases, an opening "["
    # comes afterwards. Exception: "\f[" should not be separated.
    square_bracket_pos = header.find("[")
    if square_bracket_pos == -1 or header == "\\f[":
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
        

def parse_html_tags_of_single_command(header_tag: Union[bs4.element.Tag, bs4.element.NavigableString], 
                                      description_tags: list[Union[bs4.element.Tag, bs4.element.NavigableString]]):
    description_text: list[Fragment] = []
    for desc_tag in description_tags:
        fragments = parse_recursive(desc_tag, lambda x: x)
        description_text.extend(fragments)
    description_text = strip_fragments(description_text)

    # The help page ends with "Go to the next section or return to the index.", which we currently
    # still have in the command if it is the last one. Remove it.
    for idx in range(0, len(description_text)):
        if (idx + 5 == len(description_text)
                and "Go to the" in description_text[idx].content
                and "next" in description_text[idx+1].content
                and "section or return to the" in description_text[idx+2].content):
            description_text[idx].content = description_text[idx].content.replace("Go to the", "").rstrip()
            description_text = description_text[:-4]
            break

    anchor_tag = header_tag.contents[0]
    if anchor_tag.name != "a" or "anchor" not in anchor_tag["class"] or "id" not in anchor_tag.attrs:
        raise Exception("First child is not an anchor.")
    anchor = anchor_tag["id"].strip()

    header_text = header_tag.get_text().strip()
    return ParsedCommand(header_text, anchor, description_text)


def parse_recursive(tag: bs4.element.PageElement, decorator) -> list[Fragment]:
    """ Recursively goes through all elements in the given <tag> and converts its whole content
        to a string; or more precisely, to a list of fragments since important semantic information 
        is retained.
        The <decorator> is used to allow outer tags to modify what content is added for inner tags."""

    if isinstance(tag, bs4.element.Comment):
        return []

    elif isinstance(tag, str):
        return [Fragment(FragmentType.Text, decorator(tag))]

    elif tag.name == "p":
        # Note that we pass in a decorator so that every string in the paragraph gets stripped of the newline.
        # (Newlines between paragraphs are kept!)
        fragments = parse_all_children(tag.children, lambda x: decorator(x).replace("\n", ""))
        fragments = strip_fragments(merge_fragments(fragments), " ")
        if len(fragments) > 0:
            if tag.next_sibling.name != "ul":
                fragments.append(Fragment(FragmentType.Text, "\n"))
        return fragments

    elif tag.name == "code":
        fragments = parse_all_children(tag.children, decorator)
        # In case the text in <code> contains some non-text fragments, we simply ignore that.
        # For example, the documentation for "\mainpage" contains "\ref index" in <code>, and the "\ref"
        # is additionally a hyperlink. But this is rare.
        s = "".join(f.content for f in fragments)
        return [Fragment(FragmentType.Code, s)]

    elif tag.name == "em":
        s = parse_all_children_assuming_only_text(tag.children, decorator)
        return [Fragment(FragmentType.Emphasis, s)]

    elif tag.name == "dl" and ' '.join(tag['class']) == "section see":
        if len(tag.contents) != 2:
            raise Exception("Expected the 'section see' to always have exactly 2 children.")
        see_also_fragments = parse_all_children(tag.contents[1:], decorator)

        for idx in range(0, len(see_also_fragments)):
            # The documentation often has something like "See also: Section \page for an example" which reads weird in
            # the Visual Studio autocomplete tooltips, since one cannot click there. So replace "section" with "command". 
            # But not the \section command itself. (Well, the quick info boxes actually support hyperlinks, but the 
            # autocomplete boxes do not; so simply always replace the "section".)
            see_also_fragments[idx].content = re.sub(r"\b(?<!\\)section\b", "command", see_also_fragments[idx].content)
            see_also_fragments[idx].content = re.sub(r"\b(?<!\\)Section\b", "Command", see_also_fragments[idx].content)
            see_also_fragments[idx].content = re.sub(r"\b(?<!\\)sections\b", "commands", see_also_fragments[idx].content)
            see_also_fragments[idx].content = re.sub(r"\b(?<!\\)Sections\b", "Commands", see_also_fragments[idx].content)
        
        see_also_fragments = strip_fragments(see_also_fragments)
        if len(see_also_fragments) > 0:
            see_also_fragments.insert(0, Fragment(FragmentType.Text, "See also: "))
            see_also_fragments.append(Fragment(FragmentType.Text, "\n"))

        return see_also_fragments

    elif tag.name == "dl" and ' '.join(tag['class']) in ["section note", "section warning"]:
        tag_class = ' '.join(tag['class'])

        fragments = []
        if tag.previous_sibling != "\n":
            fragments.append(Fragment(FragmentType.Text, "\n"))

        if "note" in tag_class:
            fragments.append(Fragment(FragmentType.Note, "Note:"))
        else:
            fragments.append(Fragment(FragmentType.Warning, "Warning:"))

        # For multiline notes/warnings, place the lines on dedicated lines. Otherwise, put it directly after the "Note:"/"Warning:" string.
        if len(tag.contents) > 2:
            for child in filter(lambda x: x != "\n", tag.contents[1:]):
                child_fragments = strip_fragments(parse_all_children([child], decorator))
                if len(child_fragments) > 0:
                    fragments.append(Fragment(FragmentType.Text, "\n\t"))
                    fragments.extend(child_fragments)
        else:
            children_fragments = strip_fragments(parse_all_children(tag.contents[1:], decorator))
            if len(children_fragments) > 0:
                fragments.append(Fragment(FragmentType.Text, " "))
                fragments.extend(children_fragments)

        fragments.append(Fragment(FragmentType.Text, "\n"))
        return fragments

    elif tag.name == "dl" and ' '.join(tag['class']) == "section user":
        # Either some example code, or some note
        fragments = parse_all_children(tag.children, decorator)
        fragments.append(Fragment(FragmentType.Text, "\n"))
        return fragments

    elif tag.name == "dt":
        fragments = parse_all_children(tag.children, decorator)
        fragments.append(Fragment(FragmentType.Text, " "))
        return fragments

    elif tag.name == "dd":
        fragments = merge_fragments(parse_all_children(tag.children, decorator))
        for f in fragments:
            if f.content.startswith("\n  for the corresponding HTML documentation that is generated by doxygen"):
                f.content = " " + f.content.strip()
            f.content = f.content.replace("  Click", "Click")
        return fragments

    elif tag.name == "pre" or (tag.name == "div" and ' '.join(tag['class']) == "fragment") or tag.name == "blockquote":
        # Tag contains some code example.
        s = tag.get_text().strip("\n")
        lines = [("   " + l) for l in s.split("\n")]
        concat_lines = "\n".join(lines) + "\n\n"
        if tag.previous_sibling != "\n":
            concat_lines = "\n" + concat_lines
        return [Fragment(FragmentType.Code, concat_lines)]

    elif tag.name == "li":
        nesting_level = sum(1 for p in tag.parents if p.name == "ul")
        spaces = "    " * nesting_level

        fragments = lstrip_fragments(parse_all_children(tag.children, decorator))
        fragments.insert(0, Fragment(FragmentType.Text, spaces + "• "))

        # Remove successive newlines between list elements. For example in the list in the "\showdate" command.
        if fragments[-1].content[-1] == "\n":
            fragments = rstrip_fragments(fragments, "\n")
            if tag.next_sibling == None or tag.next_sibling != "\n":
                fragments.append(Fragment(FragmentType.Text, "\n"))

        return fragments

    elif tag.name == "table":
        return parse_table(tag)

    elif tag.name == "img":
        # In the whole help page text, only one kind of image appears: Namely the LaTeX logo.
        if "LaTeX" in tag["alt"]:
            return [Fragment(FragmentType.Text, "LaTeX")]
        else:
            raise Exception("Unknown image")

    elif tag.name == "a":
        fragments = parse_all_children(tag.children, decorator)
        if "href" in tag.attrs:
            hyperlink = tag["href"]
            if not isinstance(hyperlink, str):
                raise Exception("Unexpected type for hyperlink")
            if hyperlink == "":
                raise Exception("Empty hyperlink")
            if not hyperlink.startswith("http"):
                raise Exception("Hyperlink does not start with http")
            for f in fragments:
                if f.hyperlink != "":
                    raise Exception("Found nested hyperlink")
                f.hyperlink = hyperlink
        elif "id" in tag.attrs and "name" in tag.attrs:
            pass
        elif "class" in tag.attrs and "anchor" in tag["class"] and "id" in tag.attrs:
            pass # Some anchor for other hyperlinks; ignore it.
        else:
            raise Exception("Unexpected <a> tag")

        # In case the hyperlink points to a Doxygen command, extract it as such.
        if len(fragments) == 1 and len(fragments[0].content) > 0 and fragments[0].content[0] == "\\":
            return [Fragment(FragmentType.Command, fragments[0].content, fragments[0].hyperlink)]
        else:
            return fragments

    elif tag.name == "center":
        # The "center" tag is only used for the "intermediate" headers like "Commands for displaying examples"
        # that separate the different command categories. We don't want them.
        return []

    else:
        return parse_all_children(tag.children, decorator)


def parse_all_children(children, decorator) -> list[Fragment]:
    fragments = []
    for child in children:
        fragments.extend(parse_recursive(child, decorator))
    return fragments


def parse_all_children_assuming_only_text(children, decorator) -> str:
    fragments = parse_all_children(children, decorator)
    s = ""
    for fragment in fragments:
        if fragment.type != FragmentType.Text:
            raise Exception("Expected only text fragments")
        s += fragment.content
    return s


def parse_table(table: bs4.element.Tag) -> list[Fragment]:
    """Given the <table> tag, converts the table into a properly formatted string (or rather, fragment)."""

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
    return [Fragment(FragmentType.Text, s)]


def merge_fragments(fragments: list[Fragment]) -> list[Fragment]:
    """Merges successive fragments of the same type and removes empty fragments."""

    if len(fragments) == 0:
        return []

    # Merge successive elements of the same type.
    # Note: We want to deep-copy the fragments, so as not to modify the input fragments.
    new_list = [Fragment(fragments[0].type, fragments[0].content, fragments[0].hyperlink)]
    for idx in range(1, len(fragments)):
        f = fragments[idx]
        do_merge = (
            f.type == new_list[-1].type 
            and f.hyperlink == new_list[-1].hyperlink
            # Special handling of the "Click here for the corresponding HTML documentation...": We want to keep
            # it separate, since we will want to filter it out from the tooltips in Visual Studio in case hyperlinks
            # are not shown (i.e. for the autocomplete box, but no the quick info box), because if the user cannot
            # click on the links, it makes no sense to show this message. So: 
            # 1) Do not merge the line breaks before the "Click" fragment into the "Click" fragment.
            # 2) But merge the line breaks after the "for the corresponding HTML documentation" fragment with 
            #    that fragment.
            # That way we get a nice layout if we filter out the three "Click", "here" and "for the corresponding..."
            # fragments.
            and not ("Click " in f.content 
                     and idx + 2 < len(fragments) 
                     and "here" in fragments[idx+1].content 
                     and "for the corresponding HTML documentation" in fragments[idx+2].content)
            and ("for the corresponding HTML documentation" not in new_list[-1].content or f.content.strip() == ""))

        if do_merge:
            new_list[-1].content += f.content
        else:
            new_list.append(Fragment(f.type, f.content, f.hyperlink))

    # Remove elements with empty content
    new_list = [f for f in new_list if len(f.content) > 0]
    return new_list


def lstrip_fragments(fragments: list[Fragment], to_strip: str = None) -> list[Fragment]:
    """Basically applies str.lstrip() to the start of the fragment list."""
    stripped = fragments
    while len(stripped) > 0:
        new_stripped = [Fragment(stripped[0].type, stripped[0].content.lstrip(to_strip), stripped[0].hyperlink)]
        new_stripped.extend(stripped[1:])
        new_stripped = merge_fragments(new_stripped)
        if len(stripped) == len(new_stripped):
            return new_stripped
        stripped = new_stripped
    return stripped


def rstrip_fragments(fragments: list[Fragment], to_strip: str = None) -> list[Fragment]:
    """Basically applies str.rstrip() to the end of the fragment list."""
    stripped = fragments
    while len(stripped) > 0:
        new_stripped = stripped[0:-1]
        new_stripped.append(Fragment(stripped[-1].type, stripped[-1].content.rstrip(to_strip), stripped[-1].hyperlink))
        new_stripped = merge_fragments(new_stripped)
        if len(stripped) == len(new_stripped):
            return new_stripped
        stripped = new_stripped
    return stripped


def strip_fragments(fragments: list[Fragment], to_strip: str = None) -> list[Fragment]:
    return lstrip_fragments(rstrip_fragments(fragments, to_strip), to_strip)


def generate_text_for_csharp_file(commands: list[ParsedCommand]) -> str:
    python_file = os.path.basename(__file__)
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    max_command_len, max_parameters_len, max_anchor_len = get_max_component_lengths(commands)

    s = f"// This file was automatically generated by the Python script\n"
    s += f"// {python_file} on {current_time}.\n\n"

    s += "namespace VSDoxyHighlighter\n"
    s += "{\n"
    s += "  class DoxygenCommandsGeneratedFromHelpPage\n"
    s += "  {\n"
    s += "    public static readonly DoxygenHelpPageCommand[] cCommands = {\n"

    for cmd in commands:
        command_padding = " " * (max_command_len - len(cmd.escaped_command))
        parameters_padding = " " * (max_parameters_len - len(cmd.escaped_parameters))
        anchor_padding = " " * (max_anchor_len - len(cmd.anchor))

        s += f'      new DoxygenHelpPageCommand("{cmd.escaped_command}",{command_padding} "{cmd.escaped_parameters}",{parameters_padding} "{cmd.anchor}",{anchor_padding} new (object, string, string)[]{{ '
        for fragment in cmd.escaped_help_text:
            s += f'({map_fragment_type_to_csharp_type(fragment.type)}, "{fragment.content}", "{fragment.hyperlink}"), '
        s += "}),\n"

    s += "    };\n"
    s += "  }\n"
    s += "}\n"

    return s


def map_fragment_type_to_csharp_type(type: FragmentType) -> str:
    """Returns the appropriate enumeration value to be used in the C# code."""
    if type == FragmentType.Text:
        return "null"
    elif type == FragmentType.Code:
        return "ClassificationEnum.InlineCode"
    elif type == FragmentType.Emphasis:
        return "ClassificationEnum.EmphasisMinor"
    elif type == FragmentType.Note:
        return "ClassificationEnum.Note"
    elif type == FragmentType.Warning:
        return "ClassificationEnum.Warning"
    elif type == FragmentType.Command:
        return "DoxygenHelpPageCommand.OtherTypesEnum.Command"
    else:
        raise Exception("Unknown FragmentType")


def generate_debug_dump(commands: list[ParsedCommand]) -> str:
    s = ""
    for cmd in commands:
        s += "====================================\n"
        s += f"Command: {cmd.command}\n"
        s += f"Parameters: {cmd.parameters}\n"
        s += f"Anchor: {cmd.anchor}  ==>  Hyperlink: https://www.doxygen.nl/manual/commands.html#{cmd.anchor}\n"
        s += f"Help text:\n{fragment_list_to_string_for_debug(cmd.help_text)}\n"
        s += "------------------------------------\n\n\n"
    return s


def fragment_list_to_string_for_debug(fragments: list[Fragment]) -> str:
    s = ""
    for f in fragments:
        s += "<"
        if f.type == FragmentType.Text:
            s += f.content
        elif f.type == FragmentType.Code:
            s += f"```{f.content}```"
        elif f.type == FragmentType.Emphasis:
            s += f"*{f.content}*"
        elif f.type == FragmentType.Note:
            s += f"!{f.content}!"
        elif f.type == FragmentType.Warning:
            s += f"!!!{f.content}!!!"
        elif f.type == FragmentType.Command:
            s += f"[{f.content}]"
        else:
            raise Exception("Unknown FragmentType")

        if f.hyperlink != "":
            s += f"§{f.hyperlink}§"

        s += ">"

    return s


def get_max_component_lengths(commands: list[ParsedCommand]):
    command_len = 0
    parameters_len = 0
    anchors_len = 0
    for cmd in commands:
        command_len = max(command_len, len(cmd.escaped_command))
        parameters_len = max(parameters_len, len(cmd.escaped_parameters))
        anchors_len = max(anchors_len, len(cmd.anchor))
    return (command_len, parameters_len, anchors_len)


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
    main_folder = os.path.dirname(os.path.abspath(__file__))
    extract_and_convert_doxygen_commands_from_html(
        html_filename=os.path.join(main_folder, "testInput.htm"),
        output_csharp_filename=os.path.join(main_folder, "DoxygenCommandsGeneratedFromHelpPage.cs"),
        output_debug_dump_filename=os.path.join(main_folder, "GeneratedDebugDump.txt"))
