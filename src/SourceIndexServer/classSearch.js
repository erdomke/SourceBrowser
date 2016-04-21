var classList = (function() {
  var colName = 'resultName';
  var result = new List('class-list', { valueNames: [colName] });
  var searchTerm = '';
  var origSort = result.sortFunction;
  result.sortFunction = function(b,c,d)
  {
    var bStart = b.values()[d.valueName].toLowerCase().startsWith(searchTerm) ? 0 : 1;
    var cStart = c.values()[d.valueName].toLowerCase().startsWith(searchTerm) ? 0 : 1;
    var compare = ("desc" == d.order ? -1 : 1) * (bStart < cStart ? -1 : (bStart > cStart ? 1 : 0));
    if (compare === 0) return origSort(b, c, d);
    return compare;
  };
  var origSearch = result.search;
  result.search = function(searchString, columns) 
  {
    searchTerm = searchString;
    var elem = document.getElementById('class-list');
    if (searchTerm && typeof(searchTerm) !== 'undefined' && searchTerm.length > 2)
    {
      elem.style.display = 'block';
    }
    else
    {
      elem.style.display = 'none';
    }

    var res = origSearch(searchString, columns);
    result.sort(colName);
    return res;  
  };
  return result;
})();