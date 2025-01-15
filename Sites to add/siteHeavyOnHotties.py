import PAsearchSites
import PAutils


def search(results, lang, siteNum, searchData):
    directURL = '%s/movies/%s' % (PAsearchSites.getSearchBaseURL(siteNum), slugify(searchData.title))
    searchResults = [directURL]
    directURL = '%s/movies/%s' % (PAsearchSites.getSearchBaseURL(siteNum), '-'.join(searchData.title.lower().split(' ')[1:]))
    searchResults.append(directURL)

    titleNoActors = ' '.join(searchData.title.split(' ')[2:])
    if titleNoActors.startswith('and '):
        titleNoActors = ' '.join(titleNoActors.split(' ')[3:])
    directURL = '%s/movies/%s' % (PAsearchSites.getSearchBaseURL(siteNum), slugify(titleNoActors.replace('\'', '')))
    searchResults.append(directURL)

    googleResults = PAutils.getFromGoogleSearch(searchData.title, siteNum)
    for sceneURL in googleResults:
        if '/movies/' in sceneURL and '/page-' not in sceneURL and sceneURL not in searchResults:
            searchResults.append(sceneURL)

    for sceneURL in searchResults:
        try:
            req = PAutils.HTTPRequest(sceneURL)
            scenePageElements = HTML.ElementFromString(req.text)
            titleNoFormatting = scenePageElements.xpath('//h1')[0].text_content().split(':', 1)[-1].strip().strip('\"')
            curID = PAutils.Encode(sceneURL)

            date = scenePageElements.xpath('//span[@class="released title"]/strong')
            if date:
                releaseDate = parse(date[0].text_content().strip()).strftime('%Y-%m-%d')
            else:
                releaseDate = searchData.dateFormat() if searchData.date else ''

            displayDate = releaseDate if date else ''

            if searchData.date and displayDate:
                score = 100 - Util.LevenshteinDistance(searchData.date, releaseDate)
            else:
                score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

            results.Append(MetadataSearchResult(id='%s|%d|%s' % (curID, siteNum, releaseDate), name='%s [%s] %s' % (PAutils.parseTitle(titleNoFormatting, siteNum), PAsearchSites.getSearchSiteName(siteNum), displayDate), score=score, lang=lang))
        except:
            pass

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    metadata_id = str(metadata.id).split('|')
    sceneURL = PAutils.Decode(metadata_id[0])
    sceneDate = metadata_id[2]
    if not sceneURL.startswith('http'):
        sceneURL = PAsearchSites.getSearchBaseURL(siteNum) + sceneURL
    req = PAutils.HTTPRequest(sceneURL)
    detailsPageElements = HTML.ElementFromString(req.text)

    # Title
    metadata.title = PAutils.parseTitle(detailsPageElements.xpath('//h1')[0].text_content().split(':', 1)[-1].split(' - ')[-1].strip().strip('\"'), siteNum)

    # Summary
    summary = detailsPageElements.xpath('//div[@class="video_text"]')
    if summary:
        metadata.summary = summary[0].text_content().strip()

    # Studio
    metadata.studio = "Heavy on Hotties"

    # Tagline and Collection(s)
    metadata.collections.add(metadata.studio)

    # Release Date
    date = detailsPageElements.xpath('//span[@class="released title"]/strong')
    if date:
        date_object = parse(date[0].text_content().strip())
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year
    elif sceneDate:
        date_object = parse(sceneDate)
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year

    # Actor(s)
    for actorLink in detailsPageElements.xpath('//span[@class="feature title"]//a[contains(@href, "models")]'):
        actorName = actorLink.text_content().strip()
        actorPhotoURL = ''

        actorURL = actorLink.xpath('./@href')[0]
        if not actorURL.startswith('http'):
            actorURL = '%s%s' % (PAsearchSites.getSearchBaseURL(siteNum), actorURL)
        req = PAutils.HTTPRequest(actorURL)
        actorPageElements = HTML.ElementFromString(req.text)

        try:
            actorPhotoURL = actorPageElements.xpath('//div[./h1]/img/@src')[0]
            if not actorPhotoURL.startswith('http'):
                actorPhotoURL = 'https:%s' % actorPhotoURL
        except:
            pass

        movieActors.addActor(actorName, actorPhotoURL)

    # Posters/Background
    xpaths = [
        '//video/@poster',
    ]

    for xpath in xpaths:
        for img in detailsPageElements.xpath(xpath):
            if not img.startswith('http'):
                img = 'https:' + img

            art.append(img)

    Log('Artwork found: %d' % len(art))
    for idx, posterUrl in enumerate(art, 1):
        if not PAsearchSites.posterAlreadyExists(posterUrl, metadata):
            # Download image file for analysis
            try:
                image = PAutils.HTTPRequest(posterUrl)
                im = StringIO(image.content)
                resized_image = Image.open(im)
                width, height = resized_image.size
                # Add the image proxy items to the collection
                if width > 1:
                    # Item is a poster
                    metadata.posters[posterUrl] = Proxy.Media(image.content, sort_order=idx)
                if width > 100:
                    # Item is an art item
                    metadata.art[posterUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass

    return metadata
