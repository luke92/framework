﻿
import * as React from 'react'
import { RouteComponentProps } from 'react-router'
import { Dic } from '../Globals'
import * as Finder from '../Finder'
import { ResultTable, FindOptions, FilterOption, QueryDescription } from '../FindOptions'
import { SearchMessage, JavascriptMessage } from '../Signum.Entities'
import { getQueryNiceName } from '../Reflection'
import * as Navigator from '../Navigator'
import SearchControl, { SearchControlProps } from './SearchControl'
import * as QueryString from 'query-string'

interface SearchPageProps extends RouteComponentProps<{ queryName: string }> {

}

interface SearchControlState {
    findOptions: FindOptions;
}

export default class SearchPage extends React.Component<SearchPageProps, SearchControlState> {

    constructor(props: SearchPageProps) {
        super(props);
        this.state = this.calculateState(this.props);
    }

    searchControl: SearchControl;

    componentWillReceiveProps(nextProps: SearchPageProps) {
        this.setState(this.calculateState(nextProps));
    }

    calculateState(props: SearchPageProps): SearchControlState {
        return {
            findOptions: {
                showFilters: true,
                ...Finder.parseFindOptionsPath(props.match.params.queryName, QueryString.parse(props.location.search))
            },
        };
    }

    changeUrl() {

        var scl = this.searchControl.searchControlLoaded; 

        var findOptions = Finder.toFindOptions(scl.props.findOptions, scl.props.queryDescription);

        const path = Finder.findOptionsPath(findOptions);

        Navigator.currentHistory.replace(path);
    }

    render() {
        const fo = this.state.findOptions;

        return (
            <div id="divSearchPage">
                <h2>
                    <span className="sf-entity-title">{getQueryNiceName(fo.queryName) }</span>&nbsp;
                    <a className="sf-popup-fullscreen" href="#" onClick={(e) => this.searchControl.handleFullScreenClick(e) }>
                        <span className="glyphicon glyphicon-new-window"></span>
                    </a>
                </h2>
                <SearchControl ref={(e: SearchControl) => this.searchControl = e}
                    throwIfNotFindable={true}
                    showBarExtension={true}
                    hideFullScreenButton={true}
                    largeToolbarButtons={true}
                    findOptions={fo}
                    onSearch={result => this.changeUrl()} />
            </div>
        );
    }
}



