import PAsearchSites
import PAutils


def search(results, lang, siteNum, searchData):
    searchData.encoded = urllib.quote('"%s"' % searchData.title)
    req = PAutils.HTTPRequest(PAsearchSites.getSearchSearchURL(siteNum) + searchData.encoded)
    searchResults = HTML.ElementFromString(req.text)

    searchXPath = PAutils.getDictValuesFromKey(searchXPathDB, siteNum)

    for searchResult in searchResults.xpath(searchXPath[0]):
        titleNoFormatting = searchResult.xpath(PAutils.getDictValuesFromKey(searchTitleXPathDB, siteNum)[0])[0].text_content().strip()
        if titleNoFormatting[-3:].lower() == ' 4k':
            titleNoFormatting = titleNoFormatting[:-3].strip()
        curID = PAutils.Encode(searchResult.xpath('.//a/@href')[0])

        try:
            dateXPath = PAutils.getDictValuesFromKey(searchDateXPathDB, siteNum)
            date = parse(searchResult.xpath(dateXPath[0])[-1].strip()).strftime('%Y-%m-%d')
        except:
            date = ''

        if date:
            releaseDate = date
        else:
            releaseDate = searchData.dateFormat() if searchData.date else ''

        displayDate = releaseDate if date else ''

        if searchData.date and displayDate:
            score = 100 - Util.LevenshteinDistance(searchData.date, displayDate)
        else:
            score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

        results.Append(MetadataSearchResult(id='%s|%d|%s' % (curID, siteNum, releaseDate), name='%s [%s] %s' % (PAutils.parseTitle(titleNoFormatting, siteNum), PAsearchSites.getSearchSiteName(siteNum), displayDate), score=score, lang=lang))

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    metadata_id = str(metadata.id).split('|')
    sceneURL = PAutils.Decode(metadata_id[0])
    sceneDate = ''
    if len(metadata_id) > 2:
        sceneDate = metadata_id[2]
    if not sceneURL.startswith('http'):
        sceneURL = PAsearchSites.getSearchBaseURL(siteNum) + sceneURL
    req = PAutils.HTTPRequest(sceneURL)
    detailsPageElements = HTML.ElementFromString(req.text)

    # Title
    title = detailsPageElements.xpath(PAutils.getDictValuesFromKey(titleXPathDB, siteNum)[0])[0].strip()
    if title[-3:].lower() == ' 4k':
        title = title[:-3].strip()
    metadata.title = PAutils.parseTitle(title, siteNum)

    # Summary
    metadata.summary = detailsPageElements.xpath('//p[@class="description"] | //p[@class="description-scene"] | //h2/following-sibling::p')[0].text_content().strip()

    # Studio
    metadata.studio = 'Spizoo'

    # Tagline and Collection(s)
    try:
        tagline = detailsPageElements.xpath('//i[@id="site"]/@value')[0].strip()
        metadata.tagline = tagline
    except:
        if 'Spizoo' not in PAsearchSites.getSearchSiteName(siteNum):
            tagline = PAsearchSites.getSearchSiteName(siteNum)
            metadata.tagline = tagline
        else:
            tagline = metadata.studio
    metadata.collections.add(tagline)

    # Release Date
    date = detailsPageElements.xpath('//p[@class="date"]')
    if date:
        date = date[0].text_content()[:10]
        date_object = datetime.strptime(date, '%Y-%m-%d')
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year
    elif sceneDate:
        date_object = parse(sceneDate)
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year

    # Genres
    genres = detailsPageElements.xpath('//div[@class="categories-holder"]/a|//div[./h3[contains(., "Categories")]]/a')
    if genres:
        for genreLink in genres:
            if ',' in genreLink.text_content().lower().strip():
                for genreLink in genreLink.text_content().lower().strip().split(','):
                    genreName = genreLink.strip()
                    movieGenres.addGenre(genreName)
            else:
                genreName = genreLink.text_content().lower().strip()
                movieGenres.addGenre(genreName)

    # Actor(s)
    for actorLink in detailsPageElements.xpath(PAutils.getDictValuesFromKey(actorXPathDB, siteNum)[0]):
        actorName = actorLink.text_content().replace('.', '').strip()
        actorPhotoURL = ''
        actorPageURL = actorLink.get('href')

        if 'http' not in actorPageURL:
            actorPageURL = PAsearchSites.getSearchBaseURL(siteNum) + actorLink.get('href')
        req = PAutils.HTTPRequest(actorPageURL)
        actorPage = HTML.ElementFromString(req.text)

        try:
            actorPhotoURL = actorPage.xpath(PAutils.getDictValuesFromKey(modelPhotoXPathDB, siteNum)[0])[0]
        except:
            try:
                actorPhotoURL = actorPage.xpath(PAutils.getDictValuesFromKey(modelPhotoXPathDB, siteNum)[1])[0]
            except:
                pass

        if actorPhotoURL:
            if 'http' not in actorPhotoURL:
                actorPhotoURL = PAsearchSites.getSearchBaseURL(siteNum) + actorPhotoURL

        movieActors.addActor(actorName, actorPhotoURL)

    # Posters
    xpaths = [
        '//section[@id="photos-tour"]//img[contains(@class, "update_thumb thumbs")]/@src',
        '//section[@id="scene"]//div[@id="noMore"]/img/@alt',
        '//section[@id="scene"]//video[@id="the-video"]//@poster',
        '//div[@class="row photos-holder"]//img/@src',
        '//div[@class="content-block-video"]//img/@alt',
        '//div[@id="block-content"]//img[@class]/@src',
    ]

    for xpath in xpaths:
        for img in detailsPageElements.xpath(xpath):
            if '&imgh' in img:
                img = img.split('&imgh')[0]

            art.append(img)

    Log('Artwork found: %d' % len(art))
    for idx, posterUrl in enumerate(art, 1):
        # Remove Timestamp and Token from URL
        cleanUrl = posterUrl.split('?')[0]
        art[idx - 1] = cleanUrl
        if not PAsearchSites.posterAlreadyExists(cleanUrl, metadata):
            # Download image file for analysis
            try:
                image = PAutils.HTTPRequest(posterUrl)
                im = StringIO(image.content)
                resized_image = Image.open(im)
                width, height = resized_image.size
                # Add the image proxy items to the collection
                if width > 1 or height > width:
                    # Item is a poster
                    metadata.posters[cleanUrl] = Proxy.Media(image.content, sort_order=idx)
                if width > 100 and width > height:
                    # Item is an art item
                    metadata.art[cleanUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass

    return metadata


searchXPathDB = {
    ('577', '1374', '1757'): ['//div[@class="category_listing_wrapper_updates"]'],
    ('571', '572', '573', '574', '575', '576', '293'): ['//div[@class="model-update row"]'],
    ('1825', '1826', '1827', '1828'): ['//div[@class="result-content row"]'],
}


searchTitleXPathDB = {
    ('293', '571', '572', '573', '574', '575', '576', '577', '1374', '1757'): ['.//h3'],
    ('1825', '1826', '1827', '1828'): ['.//h3/a'],
}


searchDateXPathDB = {
    ('293', '571', '572', '573', '574', '575', '576', '1374'): ['.//div[./h4[contains(., "date")]]/text()'],
    ('577', '1757'): ['.//div[./h4[contains(., "date")]]/p/text()'],
}


titleXPathDB = {
    ('293', '571', '572', '573', '574', '575', '576', '1374', '577', '1757', '1826', '1827', '1828'): ['//h1/text() | //video/@data-video'],
    ('1825'): ['//h2/text()'],
}


actorXPathDB = {
    ('293', '571', '572', '573', '574', '575', '576', '1757'): ['//h3[text()="Pornstars:"]/../a'],
    ('577'): ['//div[./h3[contains(., "playmates")]]/a'],
    ('1374'): ['//div[./h3[contains(., "Girls")]]/a'],
    ('1825'): ['//div[./h2[contains(., "GIRLS")]]//a'],
    ('1826', '1828'): ['//div[./h2[contains(., "Pornstars")]]//a'],
    ('1827'): ['//div[./h2[contains(., "Girls")]]//a'],
}


modelPhotoXPathDB = {
    ('293', '571', '572', '573', '574', '575', '576', '1374', '577', '1757'): ['//div[@class="model-bio-pic"]/img/@src', '//div[@class="model-bio-pic"]/img/@src0_1x'],
    ('1825', '1826', '1827', '1828'): ['//div[@class="model-thumbnail"]/img/@src'],
}
