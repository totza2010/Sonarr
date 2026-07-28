// Why an episode owns more than one file. A part is a piece of the episode, a version is the whole of it
// told differently; they behave identically and differ only in the marker written into the file name.
type MultipleType = 'none' | 'part' | 'version';

export default MultipleType;
